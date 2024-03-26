using ScrapRat.Util;
using SteamKit2.Internal;
using Steamworks;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Channels;
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

        public Player(ulong steamid) :
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

        public HSteamNetConnection GetConnection() =>
            this.hSteamNetConnection ??= (HSteamNetConnection)this.Interupt.RunCancelable(() => 
                SteamNetworkingSockets.ConnectP2P(ref this.NetworkingIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions));

        internal void CloseConnection(string cmsg = "You have been spied on! But don't worry, you are safe! For now...")
        {
            if (this.hSteamNetConnection.HasValue)
            {
                SteamNetworkingSockets.CloseConnection(this.hSteamNetConnection.Value, 0, cmsg, false);

                if (!this.hSteamNetConnection.HasValue)
                    return;
               
                SteamNetworkingSockets.GetConnectionInfo(this.hSteamNetConnection.Value, out var info);
                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Dead
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None)
                {
                    SteamAPI.RunCallbacks();
                    if (!this.hSteamNetConnection.HasValue)
                        return;
                    SteamNetworkingSockets.GetConnectionInfo(this.hSteamNetConnection.Value, out info);
                }

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

        internal XElement GetProfile()
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
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 1500 }
            },
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = int.MaxValue }
            },
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 4 } // 4 is public
            },
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_SDR_Penalty,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 50 }
            }
        ];
    }
}
