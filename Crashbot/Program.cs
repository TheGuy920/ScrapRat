﻿using Newtonsoft.Json;
using Steamworks;
using System.Reflection;

namespace Crashbot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);

            var steam = new SteamSocket(new SteamKit2.SteamUser.LogOnDetails()
            {
                Username = "tgo_inc",
                Password = CredentialManager.GetPassword(),
            });

            steam.Connect();

            System.Timers.Timer mainloop = new()
            {
                Interval = 100,
                AutoReset = true,
                Enabled = true
            };
            mainloop.Elapsed += (s, e) =>
            {
                SteamAPI.RunCallbacks();
            };
            mainloop.Start();

            steam.OnClientsLogin += _ =>
            {
                var steamuser = new SteamKit2.SteamID(76561198299556567);
                CSteamID cSteamID = new(steamuser.ConvertToUInt64());
                Console.WriteLine($"Logged in + [{cSteamID.m_SteamID}]");

                System.Timers.Timer t = new()
                {
                    Enabled = true,
                    AutoReset = true,
                    Interval = 50
                };

                t.Elapsed += (s, e) =>
                {
                    SteamAPI.RunCallbacks();
                    Steamworks.SteamNetworkingSockets.RunCallbacks();

                    while (SteamNetworking.IsP2PPacketAvailable(out uint size))
                    {
                        // allocate buffer and needed variables
                        var buffer = new byte[size];

                        // read the message into the buffer
                        if (SteamNetworking.ReadP2PPacket(buffer, size, out uint bytesRead, out CSteamID remoteId))
                        {
                            string message = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                            Console.WriteLine("Received a message: " + message);
                        }
                    }
                    /*
                    Steamworks.SteamNetworking.GetP2PSessionState(cSteamID, out Steamworks.P2PSessionState_t state);
                    if (state.m_bConnectionActive == 1)
                    {
                        Console.WriteLine("Connection is active");
                        Console.WriteLine(JsonConvert.SerializeObject(state, Formatting.Indented));
                    }
                    else if (state.m_eP2PSessionError != 0)
                    {
                        Console.WriteLine("Connection error: " + state.m_eP2PSessionError);
                        Console.WriteLine(JsonConvert.SerializeObject(state, Formatting.Indented));

                        t.Stop();
                        //Steamworks.SteamNetworking.CloseP2PChannelWithUser(cSteamID, 0);
                        //Steamworks.SteamNetworking.CloseP2PSessionWithUser(cSteamID);
                    }*/
                };

                t.Start();

                //Steamworks.SteamNetworking.AllowP2PPacketRelay(true);
                //var result = Steamworks.SteamNetworking.SendP2PPacket(cSteamID, [], 0, Steamworks.EP2PSend.k_EP2PSendReliable);
                //Console.WriteLine(result);

                SteamNetworkingIdentity remoteIdentity = new();
                remoteIdentity.SetSteamID(cSteamID);

               
                var res = Steamworks.SteamNetworkingSockets.InitAuthentication();
                Console.WriteLine(res);
                Steamworks.SteamNetworkingSockets.GetAuthenticationStatus(out Steamworks.SteamNetAuthenticationStatus_t status);
                Console.WriteLine(JsonConvert.SerializeObject(status, Formatting.Indented));
                var conn = Steamworks.SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 1, 0, []);
                Steamworks.SteamNetworkingSockets.FlushMessagesOnConnection(conn);
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                //Steamworks.SteamNetworking.GetP2PSessionState(cSteamID, out Steamworks.P2PSessionState_t state);
                //Console.WriteLine(JsonConvert.SerializeObject(state, Formatting.Indented));
            };

            steam.WaitForCredentials();

            while (true)
            {
                Console.ReadLine();
            }
        }
    }
}
