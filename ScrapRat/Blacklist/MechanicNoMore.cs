using ScrapRat.Util;
using Steamworks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ScrapRat.PlayerModels
{
    public class MechanicNoMore : IPlayer
    {
        private Player BasePlayer { get; }

        public string Name => this.BasePlayer.Name;

        public InteruptHandler Interupt => this.BasePlayer.Interupt;

        public CSteamID SteamID => this.BasePlayer.SteamID;

        public PrivacySettings Privacy => this.BasePlayer.Privacy;

        public ValueTask DisposeAsync() => this.BasePlayer.DisposeAsync();

        public bool AnyHostBlacklisted { get; }

        public event PlayerLoadedEventHandler? PlayerLoaded
        {
            add { this.BasePlayer.PlayerLoaded += value; }
            remove { this.BasePlayer.PlayerLoaded -= value; }
        }

        private readonly Timer LightProfileScanningThread = new()
        {
            AutoReset = true,
            Interval = 10000, // 10 seconds
            Enabled = true,
        };

        internal MechanicNoMore(Player bplayer, bool blacklistAnyHost)
        { 
            this.BasePlayer = bplayer;
            this.AnyHostBlacklisted = blacklistAnyHost;
            this.BasePlayer.OnProcessCommands += this.ProcessCommands;
            this.BasePlayer.PlayerLoaded += this.OnPlayerLoaded;
            this.LightProfileScanningThread.Elapsed += this.LightProfileScanning;
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void OnPlayerLoaded(Player player)
        {
            Task.Run(this.OpenConnection);

            if (this.Privacy >= PrivacySettings.Public)
            {
                this.LightProfileScanningThread.Start();
                this.Interupt.RunOnCancel(this.LightProfileScanningThread.Stop);
            }
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

            this.ListenForConnectionClose(connection);
        }

        private void ListenForConnectionClose(HSteamNetConnection connection)
        {
            this.Interupt.RunCancelableAsync((CancellationToken cancel) =>
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
                Task.Run(this.OpenConnection);
            }, this.Interupt.Token);
        }

        private void LightProfileScanning(object? sender, EventArgs e)
        {

        }

        private void HeavyProfileScanning(object? sender, EventArgs e)
        {

        }
    }
}
