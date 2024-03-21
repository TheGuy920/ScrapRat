using ScrapRat;
using ScrapRat.Spy;

namespace CrashWebApi
{
    public class Program
    {
        private static readonly ulong[] PeopleToTrack = [
            76561198000662213, // scrap man
            76561198004277014, // kan
            76561198079775050, // kosmo
            76561197965646622, // moonbo
            76561198299556567, // theguy920
        ];

        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            Game.Initialize();
            List<MagnifiedMechanic> targets = [];

            foreach (var steamid in PeopleToTrack)
            {
                MagnifiedMechanic mechanic = Game.BigBrother.SpyOnMechanic(steamid);
                targets.Add(mechanic);

                mechanic.PlayerLoaded += _ =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is loaded.");
                };

                mechanic.OnUpdate += @event =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is {@event}");
                };
            }

            AppDomain.CurrentDomain.ProcessExit += (object? sender, EventArgs e) =>
                targets.Select(target => target.SteamID).ToList().ForEach(steamid => Game.BigBrother.SafeUntargetPlayer(steamid));

            Task.Delay(Timeout.Infinite).Wait();
            /*
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
            */
        }
    }
}
