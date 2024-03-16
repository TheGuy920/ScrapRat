using System.Reflection;

namespace Crashbot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Console.WriteLine(Environment.CurrentDirectory);
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            Console.WriteLine(Environment.CurrentDirectory);

            var socket = new SteamSocket(new SteamKit2.SteamUser.LogOnDetails()
            {
                Username = "tgo_inc",
                Password = CredentialManager.GetPassword(),
            });

            socket.Connect();

            socket.ConnectToTarget(new SteamKit2.SteamID(76561198299556567));
        }
    }
}
