using Crashbot.Util;
using Steamworks;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;
using System.Net.Http.Headers;
using System.Timers;
using System.Xml.Linq;

namespace Crashbot.Steam
{
    public delegate void PrivacySettingsEvent(Victim.PrivacyState newPrivacy);
    public delegate void HostSteamIdEvent(CSteamID newHost);
    public delegate void VictimCrashEvent(Victim victim);
    public delegate void GameStateEvent(bool isPlaying);

    public class Victim(CSteamID steamid)
    {
        public enum PrivacyState
        {
            None = -1,
            Private = 1,
            FriendsOnly = 2,
            Public = 3
        }

        internal volatile bool IsCrashing = false;
        internal volatile bool WaitingOnRichPresence = false;

        private const int DEFAULT_INTERVAL = 60 * 1000;
        private const int FAST_INTERVAL = 1000;
        private const int DEFAULT_RICH_PRESENCE_INTERVAL = 5000;
        private readonly System.Timers.Timer trackingTimer = new()
        {
            Enabled = true,
            AutoReset = true,
            Interval = DEFAULT_INTERVAL,
        };
        private readonly System.Timers.Timer richPrecenseTimer = new()
        {
            Enabled = true,
            AutoReset = true,
            Interval = DEFAULT_RICH_PRESENCE_INTERVAL,
        };

        private PrivacyState privacySettings = PrivacyState.None;
        private CSteamID hostSteamId = new(steamid.m_SteamID);
        private bool isPlayingScrapMechanic = false;

        /// <summary>
        /// 
        /// </summary>
        public InteruptHandler Interupt { get; } = new();

        /// <summary>
        /// The current victim's SteamId64
        /// </summary>
        public CSteamID SteamId => steamid;

        /// <summary>
        /// The current victim's username.
        /// </summary>
        public string Username { get; private set; } = string.Empty;

        /// <summary>
        /// The SteamId64 of the host for the current victim.
        /// </summary>
        public CSteamID HostSteamId
        {
            get => this.hostSteamId;
            private set
            {
                if (value.m_SteamID != this.hostSteamId.m_SteamID)
                {
                    this.HostSteamIdChanged?.Invoke(value);
                    Logger.WriteLine($"Host SteamID changed to {value} for victim {Username} ({SteamId})", Verbosity.Debug);
                }
                this.hostSteamId = value;
            }
        }

        /// <summary>
        /// True if the current victim is playing Scrap Mechanic.
        /// </summary>
        public bool IsPlayingScrapMechanic
        {
            get => this.isPlayingScrapMechanic;
            private set
            {
                if (value != this.isPlayingScrapMechanic)
                {
                    this.GameStateChanged?.Invoke(value);
                    Logger.WriteLine($"Playing Scrapmechanic changed to {value} for victim {Username} ({SteamId})", Verbosity.Debug);
                }
                this.isPlayingScrapMechanic = value;
            }
        }

        /// <summary>
        /// The public profile privacy settings of the current victim.
        /// </summary>
        public PrivacyState PrivacySettings
        {
            get => this.privacySettings;
            private set
            {
                if (value != this.privacySettings)
                {
                    this.PrivacySettingsChanged?.Invoke(value);
                    Logger.WriteLine($"Privacy settings changed to {value} for victim {Username} ({SteamId})", Verbosity.Debug);
                }
                this.privacySettings = value;
            }
        }

        /// <summary>
        /// Event for when the current victim's host changes.
        /// </summary>
        public event HostSteamIdEvent? HostSteamIdChanged;

        /// <summary>
        /// The current <see cref="Victim">victim</see>'s game state changes
        /// <para>playing &lt;-&gt; not playing</para>
        /// </summary>
        public event GameStateEvent? GameStateChanged;

        /// <summary>
        /// Event for when the current victim's privacy settings change.
        /// </summary>
        public event PrivacySettingsEvent? PrivacySettingsChanged;

        /// <summary>
        /// The 'steam' display name of the current victim's privacy settings.
        /// </summary>
        public string PrivacyDisplayName { get; private set; } = string.Empty;

        /// <summary>
        /// Elapsed event for triggering the SteamInterface to collect RichPresence on this victim
        /// </summary>
        public event ElapsedEventHandler? GetRichPresence;

        /// <summary>
        /// Event for when the current victim crashes
        /// </summary>
        public event VictimCrashEvent? VictimCrashed;

        /// <summary>
        /// Starts the timer used for triggering events as to when to collect rich presence
        /// </summary>
        public void StartCollectRichPresence()
        {
            this.richPrecenseTimer.Elapsed += (e, a) =>
            {
                if (!this.WaitingOnRichPresence || true)
                {
                    this.WaitingOnRichPresence = true;
                    this.GetRichPresence?.Invoke(e, a);
                }
            };
            this.richPrecenseTimer.Start();
        }

        /// <summary>
        /// Increases the tracking interval for a certain duration.
        /// </summary>
        /// <param name="duration"></param>
        public void FasterTracking(TimeSpan duration)
        {
            this.trackingTimer.Interval = FAST_INTERVAL;
            Task.Delay(duration).ContinueWith((_) => this.trackingTimer.Interval = DEFAULT_INTERVAL);
        }

        /// <summary>
        /// Increases the tracking interval until the token is cancelled.
        /// </summary>
        /// <param name="token"></param>
        public void FasterTracking(CancellationToken token)
        {
            this.trackingTimer.Interval = FAST_INTERVAL;
            Task.Delay(Timeout.Infinite, token).ContinueWith(_ => this.trackingTimer.Interval = DEFAULT_INTERVAL);
        }

        /// <summary>
        /// Starts tracking the current victim's profile.
        /// </summary>
        public void StartTracking()
        {
            this.trackingTimer.Elapsed += this.TrackingUpdate;
            this.trackingTimer.Start();
            this.TrackingUpdate(null, null);
        }

        internal void OnVictimCrashed()
        {
            this.VictimCrashed?.Invoke(this);
        }

        internal void OnRichPresenceUpdate(Dictionary<string, string> richPresence)
        {
            this.WaitingOnRichPresence = false;

            bool probablyInGame =
                   richPresence.TryGetValue("connect", out var curl) && !string.IsNullOrWhiteSpace(curl)
                && richPresence.TryGetValue("status", out var stat) && !string.IsNullOrWhiteSpace(stat)
                && richPresence.TryGetValue("Passphrase", out var pphrase) && !string.IsNullOrWhiteSpace(pphrase);

            if (richPresence.Count <= 0 || !probablyInGame)
            {
                Logger.WriteLine($"Rich presence for {this.Username} ({this.SteamId}) is not in game", Verbosity.Debug);
                this.IsPlayingScrapMechanic = false;
                return;
            }

            Logger.WriteLine($"Rich presence for {this.Username} ({this.SteamId}) is in game", Verbosity.Debug);
            this.IsPlayingScrapMechanic = true;

            if (richPresence.TryGetValue("connect", out string? connectUrl))
            {
                ulong hostId = ulong.Parse(
                    connectUrl.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());

                this.HostSteamId = new CSteamID(hostId);
            }
        }

        internal void OnNameLoaded(string name)
        {
            if (string.IsNullOrWhiteSpace(Username))
                if (!string.IsNullOrWhiteSpace(name))
                    this.Username = name;
                else
                    throw new InvalidOperationException("Username not found");
            else
                throw new InvalidOperationException("Username already set");
        }

        private void TrackingUpdate(object? sender, ElapsedEventArgs e)
        {
            var profile = GetProfile();

            this.PrivacyDisplayName = profile.Element("privacyState")?.Value ?? string.Empty;
            this.PrivacySettings = (PrivacyState)int.Parse(profile.Element("visibilityState")?.Value ?? "-1");

            XElement? inGameInfo = profile.Element("inGameInfo");
            string gameLink = inGameInfo?.Element("gameLink")?.Value.Trim() ?? string.Empty;
            var gstate = gameLink.EndsWith("387990", StringComparison.InvariantCultureIgnoreCase);

            this.IsPlayingScrapMechanic = gstate;
        }

        private XElement GetProfile()
        {
            string url = $"https://steamcommunity.com/profiles/{steamid.m_SteamID}/?xml=1&nothing={NewUuid}";
            string response = ExecuteCurlCommand(url);
            XDocument xDocument = XDocument.Parse(response);

            return xDocument.Elements().First();
        }

        private static string NewUuid => Guid.NewGuid().ToString().Replace("-", string.Empty);

        /// <summary>
        /// This is prefered because I cannot use HttpClient due to caching issues despite custom CacheControl.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string ExecuteCurlCommand(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "curl",
                    Arguments = $"-s {url}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(startInfo);
                using StreamReader? reader = process?.StandardOutput;
                string result = reader?.ReadToEnd() ?? string.Empty;
                return result;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error: " + ex.Message, Verbosity.Normal);
                return string.Empty;
            }
        }
    }
}
