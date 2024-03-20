﻿using ScrapRat.Util;
using Steamworks;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Timers;
using System.Xml.Linq;

namespace ScrapRat
{
    public delegate void PlayerLoadedEventHandler(Player player);

    public class Player : IPlayer
    {
        public string Name { get; private set; } = string.Empty;

        public InteruptHandler Interupt { get; } = new();

        public CSteamID SteamID { get; private set; }

        public PrivacySettings Privacy { get; private set; }

        public event PlayerLoadedEventHandler? PlayerLoaded
        {
            add
            {
                if (this.IsLoaded)
                    value?.Invoke(this);
                else
                    this.playerLoaded += value;
            }
            remove
            {
                this.playerLoaded -= value;
            }
        }

        public ValueTask DisposeAsync()
        {
            this.Interupt.Dispose();
            return ValueTask.CompletedTask;
        }

        private event PlayerLoadedEventHandler? playerLoaded;
        private HSteamNetConnection? hSteamNetConnection;
        private readonly Thread CommandProcessor;
        private volatile bool IsLoaded = false;

        internal Player(ulong steamid) :
            this(new CSteamID(steamid))
        { }

        internal Player(CSteamID steamID)
        {
            this.SteamID = steamID;
            this.NetworkingIdentity = new SteamNetworkingIdentity();
            this.NetworkingIdentity.SetSteamID(this.SteamID);

            this.CommandProcessor = new Thread(this.ProcessCommands);
            this.Interupt.RunCancelableAsync(this.LoadPlayerInfo);
            this.Interupt.RunCancelableAsync(this.CommandProcessor.Start);
        }

        internal event EventHandler? OnProcessCommands;
        private void ProcessCommands()
        {
            // process commands

            this.OnProcessCommands?.Invoke(this, EventArgs.Empty);
        }

        internal SteamNetworkingIdentity NetworkingIdentity;

        internal HSteamNetConnection GetConnection() =>
            this.hSteamNetConnection ??= (HSteamNetConnection)this.Interupt.RunCancelable(() => 
                SteamNetworkingSockets.ConnectP2P(ref this.NetworkingIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions));

        internal void CloseConnection()
        {
            if (this.hSteamNetConnection.HasValue)
            {
                SteamNetworkingSockets.CloseConnection(this.hSteamNetConnection.Value, 0, "You have been spied on! You are safe, for now.", false);
                this.hSteamNetConnection = null;
            }
        }

        private void LoadPlayerInfo()
        {
            var profile = this.GetProfile();

            this.Name = profile.Element("steamID")!.Value;
            this.Privacy = profile.Element("privacyState")!.Value.FromFriendlyString();

            this.IsLoaded = true;
            this.playerLoaded?.Invoke(this);
        }

        private static string NewUuid => Guid.NewGuid().ToString().Replace("-", string.Empty);

        private readonly HttpClient RequestClient = new()
        {
            DefaultRequestHeaders = 
            {
                CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                    MaxStale = false,
                    NoTransform = true,
                    ProxyRevalidate = true,
                    MaxAge = TimeSpan.FromMilliseconds(10),
                },
            }
        };

        private XElement GetProfile()
        {
            string url = $"https://steamcommunity.com/profiles/{this.SteamID.m_SteamID}/?xml=1&nothing={NewUuid}";
            string response = this.RequestClient.GetStringAsync(url).GetAwaiter().GetResult();
            XDocument xDocument = XDocument.Parse(response);

            return xDocument.Elements().First();
        }

        private static readonly SteamNetworkingConfigValue_t[] LongTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 500 }
            },
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = int.MaxValue }
            }
        ];
    }
}
