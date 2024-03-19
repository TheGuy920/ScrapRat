using Steamworks;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;
using System.Net.Http.Headers;
using System.Timers;
using System.Xml.Linq;

namespace Crashbot
{
    public delegate void HostSteamIdEvent(CSteamID newHost);
    public delegate void PrivacySettingsEvent(Victim.PrivacyState newPrivacy);
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
            get => hostSteamId;
            private set
            {
                if (value.m_SteamID != this.hostSteamId.m_SteamID)
                {
                    this.HostSteamIdChanged?.Invoke(value);
                    Console.WriteLine($"Host SteamID changed to {value} for victim {this.Username} ({this.SteamId})", Verbosity.Debug);
                }
                this.hostSteamId = value;
            }
        }
        public event HostSteamIdEvent? HostSteamIdChanged;
        private CSteamID hostSteamId = new(steamid.m_SteamID);

        /// <summary>
        /// True if the current victim is playing Scrap Mechanic.
        /// </summary>
        public bool IsPlayingScrapMechanic
        {
            get => isPlayingScrapMechanic;
            private set
            {
                if (value != this.isPlayingScrapMechanic)
                {
                    this.GameStateChanged?.Invoke(value);
                    Console.WriteLine($"Playing Scrapmechanic changed to {value} for victim {this.Username} ({this.SteamId})", Verbosity.Debug);
                }
                this.isPlayingScrapMechanic = value;
            }
        }
        public event GameStateEvent? GameStateChanged;
        private bool isPlayingScrapMechanic = false;

        /// <summary>
        /// The public profile privacy settings of the current victim.
        /// </summary>
        public PrivacyState PrivacySettings
        {
            get => privacySettings;
            private set
            {
                if (value != this.privacySettings)
                {
                    this.PrivacySettingsChanged?.Invoke(value);
                    Console.WriteLine($"Privacy settings changed to {value} for victim {this.Username} ({this.SteamId})", Verbosity.Debug);
                }
                this.privacySettings = value;
            }
        }
        public event PrivacySettingsEvent? PrivacySettingsChanged;
        private PrivacyState privacySettings = PrivacyState.None;

        /// <summary>
        /// The 'steam' display name of the current victim's privacy settings.
        /// </summary>
        public string PrivacyDisplayName { get; private set; } = string.Empty;

        /// <summary>
        /// Current state for the victim
        /// </summary>
        public volatile bool IsCrashing = false;

        /// <summary>
        /// Rich presence is being collected for the current victim
        /// </summary>
        public volatile bool WaitingOnRichPresence = false;

        /// <summary>
        /// Elapsed event for triggering the SteamInterface to collect RichPresence on this victim
        /// </summary>
        public event ElapsedEventHandler? GetRichPresence;

        /// <summary>
        /// Starts the timer used for triggering events as to when to collect rich presence
        /// </summary>
        public void StartCollectRichPresence()
        {
            this.richPrecenseTimer.Elapsed += (e, a) =>
            {
                if (!this.WaitingOnRichPresence)
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

        /// <summary>
        /// Called when the current victim's rich presence is updated.
        /// </summary>
        /// <param name="richPresence"></param>
        public void OnRichPresenceUpdate(Dictionary<string, string> richPresence)
        {
            this.WaitingOnRichPresence = false;

            bool probablyInGame =
                   richPresence.TryGetValue("connect", out var curl) && !string.IsNullOrWhiteSpace(curl)
                && richPresence.TryGetValue("status", out var stat) && !string.IsNullOrWhiteSpace(stat)
                && richPresence.TryGetValue("Passphrase", out var pphrase) && !string.IsNullOrWhiteSpace(pphrase);

            if (richPresence.Count <= 0 || !probablyInGame)
            {
                Console.WriteLine($"Rich presence for {this.Username} ({this.SteamId}) is not in game", Verbosity.Debug);
                this.IsPlayingScrapMechanic = false;
                return;
            }

            Console.WriteLine($"Rich presence for {this.Username} ({this.SteamId}) is in game", Verbosity.Debug);
            this.IsPlayingScrapMechanic = true;

            if (richPresence.TryGetValue("connect", out string? connectUrl))
            {
                ulong hostId = ulong.Parse(
                    connectUrl.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());

                this.HostSteamId = new CSteamID(hostId);
            }
        }

        public void OnNameLoaded(string name)
        {
            if (string.IsNullOrWhiteSpace(this.Username))
                if (!string.IsNullOrWhiteSpace(name))
                    this.Username = name;
                else
                    throw new InvalidOperationException("Username not found");
            else
                throw new InvalidOperationException("Username already set");
        }

        private const int DEFAULT_INTERVAL = 60 * 1000;
        private const int FAST_INTERVAL = 2000;
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

        private bool previousGameState = false;
        private void TrackingUpdate(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var profile = this.GetProfile();

            this.PrivacyDisplayName = profile.Element("privacyState")?.Value ?? string.Empty;
            int ps = int.Parse(profile.Element("visibilityState")?.Value ?? "-1");
            this.PrivacySettings = (PrivacyState)ps;

            XElement? inGameInfo = profile.Element("inGameInfo");
            string gameLink = inGameInfo?.Element("gameLink")?.Value.Trim() ?? string.Empty;

            var gstate = gameLink.EndsWith("387990", StringComparison.InvariantCultureIgnoreCase);
            if (gstate != this.previousGameState)
                this.previousGameState = gstate;
            else
                this.IsPlayingScrapMechanic = gstate;
        }

        private readonly HttpClient session = new();
        private bool hasInitialized = false;

        private void SetWebClientSettings()
        {
            session.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue()
            {
                NoCache = true,
                NoStore = true,
                MaxAge = new TimeSpan(0),
                SharedMaxAge = new TimeSpan(0),
                NoTransform = true,
                MustRevalidate = true,
                ProxyRevalidate = true,
                MinFresh = new TimeSpan(0),
            };
            this.hasInitialized = true;
        }

        private XElement GetProfile()
        {
            if (!this.hasInitialized)
                this.SetWebClientSettings();

            this.session.CancelPendingRequests();
            string rand = GetThinUuid() + GetThinUuid() + GetThinUuid();
            string url = $"https://steamcommunity.com/profiles/{steamid.m_SteamID}?xml=1&nothing={rand}";
            // Console.WriteLine($"Fetching profile at {url}", Verbosity.Debug);

            var response = session.GetStringAsync(url).GetAwaiter().GetResult().Trim();
            XDocument xDocument = XDocument.Parse(response);

            // first xml element is <profile>
            return xDocument.Elements().First();
        }

        private static string GetThinUuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);
    }
}
