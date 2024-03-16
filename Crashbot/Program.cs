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

            var steamuser = new SteamKit2.SteamID(76561198299556567);
            CSteamID cSteamID = new(steamuser.AccountID);
            /*
            var socket = Steamworks.SteamNetworking.CreateP2PConnectionSocket(cSteamID, 1, 0, true);
            Console.WriteLine(socket.m_SNetSocket);

            Steamworks.SteamNetworking.GetP2PSessionState(cSteamID, out var state);
            Console.WriteLine(state.m_bConnectionActive);
            Console.WriteLine(state.m_nRemoteIP);
            Console.WriteLine(state.m_nRemotePort);
            Console.WriteLine(state.m_bConnecting);
            Console.WriteLine(state.m_bUsingRelay);


            byte[] data = new byte[1024];
            Steamworks.SteamNetworking.ReadP2PPacket(data, 1, out var steamID, out var channel);
            Console.WriteLine(string.Join(" ", data.Select(d => d.ToString("x2").ToUpperInvariant())));
            Console.WriteLine(steamID);
            Console.WriteLine(channel);
            */
            var result = Steamworks.SteamNetworking.SendP2PPacket(cSteamID, [1, 1], 2, Steamworks.EP2PSend.k_EP2PSendReliable);

            Console.WriteLine(result);

            //socket.ReadIncomming();
        }
    }
}
