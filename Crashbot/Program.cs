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
            var socket = Steamworks.SteamNetworking.CreateP2PConnectionSocket(cSteamID, 1, 0, true);
            Console.WriteLine(socket.m_SNetSocket);

            var result = Steamworks.SteamNetworking.SendP2PPacket(
                new Steamworks.CSteamID() { m_SteamID = steamuser.AccountID },
                [1],
                1, Steamworks.EP2PSend.k_EP2PSendReliable);

            Console.WriteLine(result);

            //socket.ReadIncomming();
        }
    }
}
