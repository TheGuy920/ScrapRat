using Steamworks;
using System.Reflection;

namespace Crashbot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Console.WriteLine(Environment.CurrentDirectory);
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);

            var socket = new SteamSocket(new SteamKit2.SteamUser.LogOnDetails()
            {
                Username = "tgo_inc",
                Password = CredentialManager.GetPassword(),
            });

            socket.Connect();

            var steamuser = new SteamKit2.SteamID(76561198299556567);
            CSteamID cSteamID = new(steamuser.AccountID);
            Steamworks.SteamNetworking.CreateP2PConnectionSocket(cSteamID, 1, 0, true);

            var result = socket.ConnectToTarget(steamuser);
            Console.WriteLine(result);

            //socket.ReadIncomming();
        }
    }
}
