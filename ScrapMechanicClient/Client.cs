using ScrapMechanic.Networking;
using ScrapMechanic.Util;
using Steamworks;
using System.Collections.Concurrent;
using System.Threading;

namespace ScrapMechanic
{
    /// <summary>
    /// Callback for when the connection status for a specific connection changes.
    /// </summary>
    /// <param name="SteamId">Steam id of the current user in question</param>
    /// <param name="Connection">Steam networking connection handle for the user</param>
    /// <param name="Info">Steam connection info container</param>
    public delegate void ConnectionStatusChanged
        (CSteamID SteamId, HSteamNetConnection Connection, SteamNetConnectionInfo_t Info);

    /// <summary>
    /// Callback for when a message is received from a connection.
    /// </summary>
    /// <param name="SteamId">Steam id of the current user in question</param>
    /// <param name="Connection">Steam networking connection handle for the user</param>
    /// <param name="Message">Steam networking message container</param>
    public delegate void ConnectionMessageReceived
        (CSteamID SteamId, HSteamNetConnection Connection, SteamNetworkingMessage_t Message);

    /// <summary>
    /// Callback for when the playstate (is/not playing) of a connection changes.
    /// </summary>
    /// <param name="SteamId"></param>
    /// <param name="Connection"></param>
    /// <param name="Playing"></param>
    public delegate void ConnectionPlaystateChanged
        (CSteamID SteamId, EPlayState Playing);

    public class Client
    {
        /// <summary>
        /// The username of the active client.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The SteamID of the active client.
        /// </summary>
        public required CSteamID SteamID { get; init; }

        /// <summary>
        /// Timeout for playstate detection. (30 seconds)
        /// </summary>
        public int PlaystateTimeout { get; set; } = 30_000;

        /// <summary>
        /// Event raised when the connection information for a specific connection changes.
        /// </summary>
        public event ConnectionStatusChanged? OnConnectionStatusChanged;

        /// <summary>
        /// Event raised when a message is received from a specific connection.
        /// </summary>
        public event ConnectionMessageReceived? OnConnectionMessageReceived;

        /// <summary>
        /// Event raised when the playstate (is/not playing) of a specific connection changes.
        /// </summary>
        public event ConnectionPlaystateChanged? OnConnectionPlaystateChanged;

        /// <summary>
        /// Initializes steam and loads the client.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public static Client Load()
        {
            if (!SteamAPI.Init())
                throw new IOException("SteamAPI.Init() failed!");

            return new() 
            {
                Name = SteamFriends.GetPersonaName(),
                SteamID = SteamUser.GetSteamID(),
            };
        }

        /// <summary>
        /// Closes all open, active, or alive connections
        /// </summary>
        public void CloseAllConnections()
        {
            foreach (var connection in this.ActiveConnections)
                SteamNetworkingSockets.CloseConnection(connection.Value, 0, "Closed by Peer", false);

            this.ActiveConnections.Clear();
        }

        /// <summary>
        /// Initiates an async connection to a specific user.
        /// </summary>
        /// <param name="cSteamID"></param>
        /// <param name="closeOldConnection"></param>
        public void ConnectToUserAsync(CSteamID cSteamID, bool closeOldConnection = true)
        {
            NetworkConnection connection = this.ActiveConnections.GetOrAdd(cSteamID, NewConnection);
            this.ActiveConnections.TryAdd(cSteamID, connection);
            connection.ConnectP2P(0, LongTimeoutOptions, false, closeOldConnection);
        }

        /// <summary>
        /// Initiates a connection to a specific user.
        /// </summary>
        /// <param name="cSteamID"></param>
        public void ConnectToUser(CSteamID cSteamID)
        {
            NetworkConnection connection = this.ActiveConnections.GetOrAdd(cSteamID, NewConnection);
            this.ActiveConnections.TryAdd(cSteamID, connection);
            connection.ConnectP2P(0, LongTimeoutOptions).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cSteamID"></param>
        public void CloseConnection(CSteamID cSteamID)
        {
            if (this.ActiveConnections.TryRemove(cSteamID, out var connection))
                connection.CloseConnection(ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Invalid, "Closed by Peer", false);
        }

        private readonly Thread RunUpdatesThread;
        private readonly CancellationTokenSource CancellationToken;
        private readonly ConcurrentDictionary<CSteamID, NetworkConnection> ActiveConnections = [];
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
        ];

        internal Client()
        {
            this.CancellationToken = new();
            this.RunUpdatesThread = new(RunUpdates) { IsBackground = false, Priority = ThreadPriority.Highest };
            this.RunUpdatesThread.Start();
        }

        ~Client()
        {
            this.CancellationToken.Cancel();
            this.CloseAllConnections();
            this.RunUpdatesThread.Join();
            SteamAPI.Shutdown();
        }

        private async void RunUpdates()
        {
            while (!this.CancellationToken.IsCancellationRequested)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(5);
            }
        }

        private readonly ConcurrentDictionary<CSteamID, PlayStateHolder> PlayStates = [];

        private NetworkConnection NewConnection(CSteamID steamid)
        {
            var connection = new NetworkConnection(steamid);
            connection.OnConnectionStatusChanged += this.ConnectionTracking;
            connection.OnConnectionStatusChanged += (_, __, ___) => this.OnConnectionStatusChanged?.Invoke(_, __, ___);
            connection.OnConnectionMessageReceived += (_, __, ___) => this.OnConnectionMessageReceived?.Invoke(_, __, ___);
            this.PlayStates.TryAdd(steamid, new() 
            { 
                ConnectionTimeoutTimer = new(ConnectionTimeout, steamid, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan) 
            });

            return connection;
        }

        private void ConnectionTimeout(object? cstid)
        {
            CSteamID steamID = (CSteamID)cstid!;
            if (this.PlayStates.TryGetValue(steamID, out var playState))
            {
                if (playState.CurrentPlayState != EPlayState.Playing)
                    return;

                playState.CurrentPlayState = EPlayState.NotPlaying;
                this.OnConnectionPlaystateChanged?.Invoke(steamID, EPlayState.NotPlaying);
            }
        }

        private void ConnectionTracking(CSteamID steamID, HSteamNetConnection hconn, SteamNetConnectionInfo_t Info)
        {
            var playState = this.PlayStates[steamID];
            var connectionState = Info.m_eState.ToConnectionState();

            switch (connectionState)
            {
                case ConnectionState.None:
                    Logger.LogError($"Connection to {steamID} is in an unknown state.");
                    break;
                case ConnectionState.Connecting:
                    if (playState.CurrentPlayState == EPlayState.Dead)
                    {
                        playState.CurrentPlayState = EPlayState.NotPlaying;
                        return;
                    }

                    // starts the timeout timer
                    Logger.LogWarning($"Timeout Timer due in: {this.PlaystateTimeout / 1000d:F3}s");
                    playState.ConnectionTimeoutTimer!.Change(this.PlaystateTimeout, Timeout.Infinite);

                    if (playState.CurrentPlayState == EPlayState.NotPlaying)
                    {
                        playState.CurrentPlayState = EPlayState.Playing;
                        this.OnConnectionPlaystateChanged?.Invoke(steamID, EPlayState.Playing);
                    }
                    break;
                case ConnectionState.Connected:
                    // verify connection was notified
                    if (playState.CurrentPlayState == EPlayState.NotPlaying)
                    {
                        playState.CurrentPlayState = EPlayState.Playing;
                        this.OnConnectionPlaystateChanged?.Invoke(steamID, EPlayState.Playing);
                    }

                    Logger.LogWarning("Timeout Timer due in: Timeout.Infinite");
                    playState.ConnectionTimeoutTimer!.Change(Timeout.Infinite, Timeout.Infinite);
                    break;
                case ConnectionState.Disconnected:
                    if (playState.LastSteamConnectionState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    {
                        playState.CurrentPlayState = EPlayState.Dead;
                        this.OnConnectionPlaystateChanged?.Invoke(steamID, EPlayState.Dead);
                    }

                    Logger.LogWarning("Timeout Timer due in: Timeout.Infinite");
                    playState.ConnectionTimeoutTimer!.Change(Timeout.Infinite, Timeout.Infinite);
                    break;
            }
            playState.LastSteamConnectionState = Info.m_eState;
        }
    }
}
