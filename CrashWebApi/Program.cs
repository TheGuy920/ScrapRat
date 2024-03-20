using ScrapRat;

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
            foreach (var steamid in PeopleToTrack)
            {
                var player = Game.Spy.TargetPlayer(steamid);
                player.PlayerLoaded += _ =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{player.Name}' ({player.SteamID}) is loaded.");
                };

                player.OnUpdate += @event =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{player.Name}' ({player.SteamID}) is {@event}");
                };
            }

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
