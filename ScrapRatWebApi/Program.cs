using ScrapRat;
using ScrapRat.PlayerModels;
using ScrapRatWebApi.Discord;

namespace ScrapRatWebApi
{
    public class Program
    {
        private static readonly ulong[] PeopleToTrack = [
            76561198000662213, // scrap man
            76561198004277014, // kan
            76561198079775050, // kosmo
            76561197965646622, // moonbo
        ];

        private static readonly Dictionary<ulong, ulong> SteamidToDiscordid = new()
        {
            {76561198000662213, 185907632769466368},
            {76561198004277014, 162679241857564673},
            {76561198079775050, 239167036205301761},
            {76561197965646622, 143945560368480256},
        };

        private static readonly DiscordWebhook _webhook = new(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "webhook.url"));

        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            ScrapMechanic.Initialize();
            List<MagnifiedMechanic> targets = [];

            foreach (var steamid in PeopleToTrack)
            {
                MagnifiedMechanic mechanic = ScrapMechanic.BigBrother.SpyOnMechanic(steamid);
                targets.Add(mechanic);

                mechanic.PlayerLoaded += _ =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is loaded.");
                };

                mechanic.OnUpdate += @event =>
                {
                    Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is {@event}");
                    
                    string discordid = SteamidToDiscordid.TryGetValue(steamid, out ulong id) ? $"<@{id}>" : mechanic.Name;
                    switch (@event)
                    {
                        case ObservableEvent.NowPlaying:
                            _webhook.SendMessage($"@everyone {discordid} is now playing Scrap Mechanic!");
                            break;
                        case ObservableEvent.StoppedPlaying:
                            _webhook.SendMessage($"@everyone {discordid} stopped playing Scrap Mechanic :(");
                            break;
                    }
                };
            }

            AppDomain.CurrentDomain.ProcessExit += (object? sender, EventArgs e) =>
                targets.Select(target => target.SteamID).ToList().ForEach(steamid => ScrapMechanic.BigBrother.SafeUntargetPlayer(steamid));

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
