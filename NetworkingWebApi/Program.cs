using Discord;
using ScrapMechanic.Networking;
using ScrapMechanic.Util;
using ScrapMechanic.WebApi.Discord;
using Steamworks;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace ScrapMechanic.WebApi
{
    public class Program
    {
        private static readonly ulong[] PeopleToTrack = [
            // 76561198422873503, // unknown
            76561198000662213, // scrap man
            76561198004277014, // kan
            76561198079775050, // kosmo
            76561197965646622, // moonbo
            76561198014346778, // Durf (fuck you)
            76561198018743729, // Fant (fuck you)
            // 76561198299556567, // theguy920
        ];

        private static readonly Dictionary<ulong, ulong> SteamidToDiscordid = new()
        {
            { 76561198000662213, 185907632769466368 },
            { 76561198004277014, 162679241857564673 },
            { 76561198079775050, 239167036205301761 },
            { 76561197965646622, 143945560368480256 },
            { 76561198299556567, 333609235579404288 },
            { 76561198014346778, 189887027355844608 },
            { 76561198018743729, 318822174557339659 }
        };
        
        private static readonly DiscordWebhook _webhook = new(
            File.ReadAllText(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "webhook.url")
                ));
        
        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            var client = ScrapMechanic.Client.Load();
            List<CSteamID> cstids = PeopleToTrack.Select(id => new CSteamID(id)).ToList();
            ConcurrentDictionary<CSteamID, string> nameMaps = [];

            string getNameString(CSteamID cstid)
                => SteamidToDiscordid.TryGetValue(cstid.m_SteamID, out ulong id) ? $"<@{id}> `{nameMaps[cstid]}`" : nameMaps[cstid];

            void pushUpdate(CSteamID cstid, EPlayState isPlaying)
            {
                var name = nameMaps[cstid];
                string discordid = getNameString(cstid);
                string state = isPlaying == EPlayState.Playing 
                    ? "is now playing Scrap Mechanic!" : "stopped playing Scrap Mechanic :(";
                
                ScrapMechanic.Logger.LogWarning($"Player '{name}' ({cstid}) {state}");
                //_webhook.SendMessage($"@everyone {discordid} {state}");
            }

            client.OnConnectionPlaystateChanged += pushUpdate;
            client.OnConnectionStatusChanged += (cstid, _, info) =>
            {
                if (!info.IsConnectionAlive())
                    client.ConnectToUserAsync(cstid);
            };

            foreach (var cstid in cstids)
            {
                nameMaps[cstid] = GetName(cstid);
                client.ConnectToUserAsync(cstid);

                Logger.LogInfo($"Now tracking: {getNameString(cstid)}");
                // _webhook.SendMessage($"Now tracking: {getNameString(cstid)}");
            }

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

        static readonly HttpClient RequestClient = new()
        {
            DefaultRequestHeaders =
            {
                CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                    MaxStale = false,
                    NoTransform = true,
                    ProxyRevalidate = true,
                    MaxAge = TimeSpan.FromMilliseconds(10),
                },
            }
        };

        static string GetName(CSteamID stid) => WebUtility.HtmlDecode(GetProfile(stid).Element("steamID")!.Value);

        static string NewUuid => Guid.NewGuid().ToString().Replace("-", string.Empty);

        static XElement GetProfile(CSteamID stid)
        {
            string url = $"https://steamcommunity.com/profiles/{stid}/?xml=1";
            string response = RequestClient.GetStringAsync(url).GetAwaiter().GetResult();
            XDocument xDocument = XDocument.Parse(response);

            return xDocument.Elements().First();
        }
    }
}
