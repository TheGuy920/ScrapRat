﻿using Steamworks;
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
                    Console.WriteLine($"Host SteamID changed to {value} for victim {this.Username} ({this.SteamId})");
                }
                this.hostSteamId = value;
            }
        }
        public event HostSteamIdEvent? HostSteamIdChanged;
        private CSteamID hostSteamId;

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
                    Console.WriteLine($"Playing Scrapmechanic changed to {value} for victim {this.Username} ({this.SteamId})");
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
                    Console.WriteLine($"Privacy settings changed to {value} for victim {this.Username} ({this.SteamId})");
                }
                this.privacySettings = value;
            }
        }
        public event PrivacySettingsEvent? PrivacySettingsChanged;
        private PrivacyState privacySettings = PrivacyState.None;

        /// <summary>
        /// The 'steam' display name of the current victim's privacy settings.
        /// </summary>
        public string PrivacyDisplayName { get; private set; }

        /// <summary>
        /// Current state for the victim
        /// </summary>
        public volatile bool IsCrashing;

        /// <summary>
        /// Elapsed event for triggering the SteamInterface to collect RichPresence on this victim
        /// </summary>
        public event ElapsedEventHandler? GetRichPresence;

        /// <summary>
        /// Starts the timer used for triggering events as to when to collect rich presence
        /// </summary>
        public void StartCollectRichPresence()
        {
            this.richPrecenseTimer.Elapsed += this.GetRichPresence;
            this.richPrecenseTimer.Start();
        }

        /// <summary>
        /// Increases the tracking interval for a certain duration.
        /// </summary>
        /// <param name="duration"></param>
        public void FasterTracking(TimeSpan duration)
        {
            this.trackingTimer.Interval = 1000;
            Task.Delay(duration).ContinueWith((_) => this.trackingTimer.Interval = DEFAULT_INTERVAL);
        }

        /// <summary>
        /// Increases the tracking interval until the token is cancelled.
        /// </summary>
        /// <param name="token"></param>
        public void FasterTracking(CancellationToken token)
        {
            this.trackingTimer.Interval = 1000;
            Task.Delay(TimeSpan.MaxValue, token).ContinueWith((_) => this.trackingTimer.Interval = DEFAULT_INTERVAL);
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
            bool probablyInGame =
                   richPresence.TryGetValue("connect", out var curl) && !string.IsNullOrWhiteSpace(curl)
                && richPresence.TryGetValue("status", out var stat) && !string.IsNullOrWhiteSpace(stat)
                && richPresence.TryGetValue("Passphrase", out var pphrase) && !string.IsNullOrWhiteSpace(pphrase);

            if (richPresence.Count <= 0 || !probablyInGame)
            {
                this.IsPlayingScrapMechanic = false;
                return;
            }

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
        private const int DEFAULT_RICH_PRESENCE_INTERVAL = 2000;
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

        private void TrackingUpdate(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var profile = this.GetProfile();

            // now we look for the <privacyState> and <visibilityState> elements
            this.PrivacyDisplayName = profile.Element("privacyState")?.Value ?? string.Empty;
            int ps = int.Parse(profile.Element("visibilityState")?.Value ?? "-1");
            this.PrivacySettings = (PrivacyState)ps;

            // now we look for <inGameInfo> and then <gameLink>
            XElement? inGameInfo = profile.Element("inGameInfo");
            string gameLink = inGameInfo?.Element("gameLink")?.Value.Trim() ?? string.Empty;
            this.IsPlayingScrapMechanic = gameLink.EndsWith("387990", StringComparison.InvariantCultureIgnoreCase);
        }

        private XElement GetProfile()
        {
            string url = "https://steamcommunity.com/profiles/" + steamid.m_SteamID + "?xml=true";
            var httpresponse = new HttpClient().GetAsync(url).GetAwaiter().GetResult();
            string response = httpresponse.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim();
            XDocument xDocument = XDocument.Parse(response);

            // first xml element is <profile>
            return xDocument.Elements().First();
        }
    }
}
