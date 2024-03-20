using ScrapRat.Util;
using Steamworks;
using static SteamKit2.GC.Dota.Internal.CMsgDOTALeague;
using System.Diagnostics;
using System.Threading.Channels;

namespace ScrapRat.Spy
{
    public class SpyTarget : IPlayer
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

        internal SpyTarget(Player player)
        {
            this.BasePlayer = player;
            this.BasePlayer.OnProcessCommands += this.ProcessCommands;
            this.BasePlayer.PlayerLoaded += this.OnPlayerLoaded;
        }

        private void OnPlayerLoaded(Player player)
        {
            this.OpenConnection();
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
                        SteamNetworkingSockets.CloseConnection(connection, 0, "Cancelled", false);
                        SteamAPI.RunCallbacks();
                        return;
                    }
                }
            }, this.Interupt.Token);

            this.OnUpdate?.Invoke(ObservableEvent.NowPlaying);

            this.ListenForConnectionClose();
        }

        private void ListenForConnectionClose()
        {
            var connection = this.BasePlayer.GetConnection();

            this.Interupt.RunCancelableAsync((CancellationToken cancel) =>
            {
                SteamNetworkingSockets.GetConnectionInfo(connection, out var info);

                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    || info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                    || info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Dead)
                {
                    SteamAPI.RunCallbacks();
                    SteamNetworkingSockets.GetConnectionInfo(connection, out info);

                    if (cancel.CanBeCanceled == true && cancel.IsCancellationRequested == true)
                    {
                        SteamNetworkingSockets.CloseConnection(connection, 0, "Cancelled", false);
                        SteamAPI.RunCallbacks();
                        return;
                    }
                }

                this.OnUpdate?.Invoke(ObservableEvent.StoppedPlaying);
                Task.Run(this.OpenConnection);
            }, this.Interupt.Token);
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public event ObservableEventHandler? OnUpdate;
    }
}
