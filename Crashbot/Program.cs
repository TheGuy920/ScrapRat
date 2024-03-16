using Newtonsoft.Json;
using Steamworks;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json.Serialization;

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
                };

                t.Start();

                Steamworks.SteamNetworking.AllowP2PPacketRelay(true);
                var result = Steamworks.SteamNetworking.SendP2PPacket(cSteamID, [1], 1, Steamworks.EP2PSend.k_EP2PSendReliable, 1);
                Console.WriteLine(result);

                Steamworks.SteamNetworking.AcceptP2PSessionWithUser(cSteamID);

                Steamworks.SteamNetworking.GetP2PSessionState(cSteamID, out Steamworks.P2PSessionState_t state);
                Console.WriteLine(JsonConvert.SerializeObject(state, Formatting.Indented));

                
            };

            steam.WaitForCredentials();

            while (true)
            {
                Console.ReadLine();
            }
        }
    }
}
