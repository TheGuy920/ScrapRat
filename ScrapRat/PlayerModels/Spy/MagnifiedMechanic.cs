using ScrapRat.Util;
using Steamworks;
using System.Diagnostics;
using System.Net;
using Console = ScrapRat.Util.Console;

namespace ScrapRat.PlayerModels
{
    public class MagnifiedMechanic : IPlayer
    {
        // 5 seconds
        private const int RECONNECT_TIMEOUT = 5000;
        // 25 seconds
        private const int RECONNECT_DELAY = 25000;
        // 5 seconds
        private const int CONNECTION_THRESHOLD = 5000;

        public string Name => this.BasePlayer.Name;
        public InteruptHandler Interupt => this.BasePlayer.Interupt;
        public CSteamID SteamID => this.BasePlayer.SteamID;
        public PrivacySettings Privacy => this.BasePlayer.Privacy;
        public event PlayerLoadedEventHandler? PlayerLoaded
        {
            add => this.BasePlayer.PlayerLoaded += value;
            remove => this.BasePlayer.PlayerLoaded -= value;
        }
        public ValueTask DisposeAsync() => this.BasePlayer.DisposeAsync();
        public bool IsWordPublic { get; private set; }

        private Player BasePlayer { get; }
        private readonly InteruptHandler ConnectionDuration = new();

        internal MagnifiedMechanic(Player player)
        {
            this.BasePlayer = player;
            this.BasePlayer.OnProcessCommands += this.ProcessCommands;
            this.BasePlayer.PlayerLoaded += this.OnPlayerLoaded;
        }

        private void OnPlayerLoaded(Player player)
        {
            Task.Run(() => this.OpenConnection());
        }

        private void OpenConnection(bool hasStoppedPlaying = true)
        {
            var connection = this.BasePlayer.GetConnection();

            if (!hasStoppedPlaying)
            {
                Console.WarnLine($"[{DateTime.Now}] Player '{WebUtility.HtmlDecode(this.Name)}' ({this.SteamID}) was disconnected. " +
                    $"Waiting for {RECONNECT_TIMEOUT/1000} seconds to see if they stopped playing.");
                this.ConnectionDuration.RunCancelableAsync((CancellationToken tok) => Task.Delay(RECONNECT_TIMEOUT, tok).ContinueWith(_ =>
                {
                    if (tok.IsCancellationRequested)
                        return;
                    this.OnUpdate?.Invoke(ObservableEvent.StoppedPlaying);
                    hasStoppedPlaying = true;
                }, tok).Wait(tok), this.ConnectionDuration.Token);
            }

            Stopwatch connectionDuration = Stopwatch.StartNew();
            this.Interupt.RunCancelable((CancellationToken cancel) =>
            {
                SteamNetworkingSockets.GetConnectionInfo(connection, out var info);

                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
                {
                    SteamAPI.RunCallbacks();
                    SteamNetworkingSockets.GetConnectionInfo(connection, out info);

                    if (cancel.CanBeCanceled == true && cancel.IsCancellationRequested == true)
                    {
                        this.BasePlayer.CloseConnection();
                        return;
                    }

                    Task.Delay(1, cancel).Wait(cancel);
                }
            }, this.Interupt.Token);

            Console.InfoLine($"[{DateTime.Now}] Player '{WebUtility.HtmlDecode(this.Name)}' ({this.SteamID}) connected in {connectionDuration.Elapsed.TotalSeconds} seconds.");

            if (hasStoppedPlaying)
                this.OnUpdate?.Invoke(ObservableEvent.NowPlaying);
            else
                this.ConnectionDuration.Reset();

            this.ListenForConnectionClose(connection);
        }

        private void ListenForConnectionClose(HSteamNetConnection connection)
        {
            Stopwatch connectionDuration = Stopwatch.StartNew();
            this.Interupt.RunCancelable((CancellationToken cancel) =>
            {
                SteamNetworkingSockets.GetConnectionInfo(connection, out var info);

                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Dead
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None)
                {
                    SteamAPI.RunCallbacks();
                    SteamNetworkingSockets.GetConnectionInfo(connection, out info);

                    if (cancel.CanBeCanceled == true && cancel.IsCancellationRequested == true)
                    {
                        this.BasePlayer.CloseConnection();
                        return;
                    }

                    Task.Delay(1, cancel).Wait(cancel);
                }

                this.BasePlayer.CloseConnection();
            }, this.Interupt.Token);

            Console.InfoLine($"[{DateTime.Now}] Player '{WebUtility.HtmlDecode(this.Name)}' ({this.SteamID}) disconnected. " +
                $"Connection lasted {connectionDuration.Elapsed.TotalSeconds} seconds.");

            // longer than X seconds connected, followed by a disconnected is treated as stopped playing
            if (connectionDuration.Elapsed.TotalMilliseconds > CONNECTION_THRESHOLD)
            {
                this.OnUpdate?.Invoke(ObservableEvent.StoppedPlaying);
                this.OpenConnection(true);
                return;
            }

            // Else, the game mode IS private, so we try to connect again
            Console.WarnLine($"[{DateTime.Now}] Player '{WebUtility.HtmlDecode(this.Name)}' ({this.SteamID}) has a private game mode. " +
                $"Trying to reconnect in {RECONNECT_DELAY/1000} seconds.");

            Task.Delay(RECONNECT_DELAY).Wait();
            this.OpenConnection(false);
            
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        public event ObservableEventHandler? OnUpdate;
    }
}
