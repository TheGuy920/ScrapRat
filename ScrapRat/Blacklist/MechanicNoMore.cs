using ScrapRat.Util;
using Steamworks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ScrapRat.PlayerModels
{
    public class MechanicNoMore : IPlayer
    {
        private Player BasePlayer { get; }

        private Player Host { get; set; }

        private bool isBlacklisted = true;
        public bool IsBlacklisted
        {
            get => this.isBlacklisted;
            set
            {
                if (value != this.isBlacklisted)
                {
                    this.Interupt.Reset();
                    this.OnPlayerLoaded(this.BasePlayer);
                    this.isBlacklisted = value;
                }
            }
        }

        public string Name => this.BasePlayer.Name;

        public InteruptHandler Interupt => this.BasePlayer.Interupt;

        public CSteamID SteamID => this.BasePlayer.SteamID;

        public PrivacySettings Privacy => this.BasePlayer.Privacy;

        public ValueTask DisposeAsync() => this.BasePlayer.DisposeAsync();

        public bool AnyHostBlacklisted { get; }

        public bool IsInGame { get; private set; }

        public event PlayerLoadedEventHandler? PlayerLoaded
        {
            add { this.BasePlayer.PlayerLoaded += value; }
            remove { this.BasePlayer.PlayerLoaded -= value; }
        }

        private const int SUPER_SLOW_SCAN = 60000; // 60 seconds
        private const int SLOW_SCAN = 10000; // 10 seconds
        private const int QUICK_SCAN = 2000; // 2 seconds
        private const int FAST_SCAN = 1000; // 1 second

        private readonly Timer ScanningTimer = new()
        {
            AutoReset = true,
            Interval = SLOW_SCAN,
            Enabled = false,
        };
        
        private readonly Timer RichPresenceTimer = new()
        {
            AutoReset = true,
            Interval = SUPER_SLOW_SCAN,
            Enabled = false,
        };

        internal MechanicNoMore(Player bplayer, bool blacklistAnyHost)
        {
            this.Host = bplayer;
            this.BasePlayer = bplayer;
            this.AnyHostBlacklisted = blacklistAnyHost;
            this.BasePlayer.OnProcessCommands += this.ProcessCommands;
            this.BasePlayer.PlayerLoaded += this.OnPlayerLoaded;
            this.ScanningTimer.Elapsed += this.ProfileScanning;
            this.RichPresenceTimer.Elapsed += this.UpdateRichPresence;
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void OnPlayerLoaded(Player player)
        {
            this.Host = this.BasePlayer;
            Task.Run(this.OpenConnection);

            if (this.Privacy >= PrivacySettings.Public && this.AnyHostBlacklisted)
            {
                this.ScanningTimer.Interval = SLOW_SCAN;
                this.RichPresenceTimer.Interval = SUPER_SLOW_SCAN;

                this.ScanningTimer.Start();
                this.RichPresenceTimer.Start();

                this.Interupt.RunOnCancel(this.ScanningTimer.Stop);
                this.Interupt.RunOnCancel(this.RichPresenceTimer.Stop);
            }
        }

        private void OpenConnection()
        {
            var connection = this.Host.GetConnection();

            Console.WriteLine("Opening connection...");
            var result = this.Interupt.RunCancelable((CancellationToken cancel) =>
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
                        this.Host.CloseConnection();
                        return false;
                    }
                }

                return info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
            }, this.Interupt.Token);

            if (result)
                this.ListenForOneMsgAndCrash(connection);
            else
                this.OpenConnection();
        }

        private void ListenForOneMsgAndCrash(HSteamNetConnection connection)
        {
            this.Interupt.RunCancelable((CancellationToken cancel) =>
            {
                SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, new IntPtr[1], 1);

                if (this.IsBlacklisted)
                    SteamNetworkingSockets.SendMessageToConnection(connection, 0, 0, 0, out long _);
                SteamAPI.RunCallbacks();

            }, this.Interupt.Token);

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
                        this.Host.CloseConnection();
                        return;
                    }
                }

                this.Host.CloseConnection();
            }, this.Interupt.Token);

            this.OpenConnection();
        }

        private readonly List<bool> previous_game_states = new(3);
        private void ProfileScanning(object? sender, EventArgs e)
        {
            var profile = this.BasePlayer.GetProfile();
            
            var ginfo = profile.Element("inGameInfo");
            if (ginfo != null && ginfo.Element("gameLink")!.Value.EndsWith("387990"))
            {
                if (this.previous_game_states.Count >= 3)
                    this.previous_game_states.RemoveAt(0);
                this.previous_game_states.Add(true);

                if (this.previous_game_states.All(b => b) && !this.IsInGame)
                { 
                    Console.WriteLine("Player is in game, scanning fast!");
                    this.ScanningTimer.Interval = FAST_SCAN;
                    this.RichPresenceTimer.Interval = QUICK_SCAN;
                    this.IsInGame = true;
                }

                return;
            }

            if (this.previous_game_states.Count >= 3)
                this.previous_game_states.RemoveAt(0);
            this.previous_game_states.Add(false);

            if (this.IsInGame)
            {
                Console.WriteLine("Player is not in game, scanning slow!");
                this.ScanningTimer.Interval = SLOW_SCAN;
                this.RichPresenceTimer.Interval = SUPER_SLOW_SCAN;
                this.IsInGame = false;
            }
        }

        private void UpdateRichPresence(object? sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Updating rich presence...");
            SteamFriends.RequestFriendRichPresence(this.BasePlayer.SteamID);
            SteamAPI.RunCallbacks();
            Thread.Sleep(100);

            var richPresence = this.LoadUserRP(this.SteamID);

            Console.WriteLine(string.Join(", ", richPresence.SelectMany(s => $"{s.Key}: {s.Value}")));

            bool isInAWorld =
                   richPresence.TryGetValue("connect", out var curl) && !string.IsNullOrWhiteSpace(curl)
                && richPresence.TryGetValue("status", out var stat) && !string.IsNullOrWhiteSpace(stat)
                && richPresence.TryGetValue("Passphrase", out var pphrase) && !string.IsNullOrWhiteSpace(pphrase);

            if (richPresence.Count <= 0 || !isInAWorld)
                return;

            if (richPresence.TryGetValue("connect", out string? connectUrl))
            {
                ulong hostId = ulong.Parse(
                    connectUrl.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());

                if (this.Host.SteamID.m_SteamID != hostId)
                {
                    Console.WriteLine("Host changed, reconnecting...");
                    this.Host = new Player(hostId);
                    this.Interupt.Reset();
                    Task.Run(this.OpenConnection);
                }
            }
        }

        private Dictionary<string, string> LoadUserRP(CSteamID user)
        {
            Dictionary<string, string> richPresence = [];

            int keyCount = SteamFriends.GetFriendRichPresenceKeyCount(user);

            if (keyCount > 0)
            {
                for (int i = 0; i < keyCount; i++)
                {
                    string key = SteamFriends.GetFriendRichPresenceKeyByIndex(user, i)!;
                    string value = SteamFriends.GetFriendRichPresence(user, key)!;
                    richPresence.Add(key, value);
                }
            }

            return richPresence;
        }
    }
}
