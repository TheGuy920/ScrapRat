using ScrapRat.Util;
using Steamworks;

namespace ScrapRat.PlayerModels
{
    public class MagnifiedMechanic : IPlayer
    {
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

        private Player BasePlayer { get; }

        internal MagnifiedMechanic(Player player)
        {
            this.BasePlayer = player;
            this.BasePlayer.OnProcessCommands += this.ProcessCommands;
            this.BasePlayer.PlayerLoaded += this.OnPlayerLoaded;
        }

        private void OnPlayerLoaded(Player player)
        {
            Task.Run(this.OpenConnection);
        }

        private void OpenConnection()
        {
            var connection = this.BasePlayer.GetConnection();

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
                }
            }, this.Interupt.Token);

            this.OnUpdate?.Invoke(ObservableEvent.NowPlaying);
            this.ListenForConnectionClose(connection);
        }

        private void ListenForConnectionClose(HSteamNetConnection connection)
        {
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
                }

                this.BasePlayer.CloseConnection();
            }, this.Interupt.Token);

            this.OnUpdate?.Invoke(ObservableEvent.StoppedPlaying);
            this.OpenConnection();
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        public event ObservableEventHandler? OnUpdate;
    }
}
