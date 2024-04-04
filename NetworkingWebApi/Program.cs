using Discord;
using ScrapMechanic.Networking;
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
            76561198299556567, // theguy920
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
            ConcurrentDictionary<CSteamID, Timer> connectionTimeoutTimers = [];
            ConcurrentDictionary<CSteamID, int> connectionStates = [];
            ConcurrentDictionary<CSteamID, string> nameMaps = [];

            const int TIMEOUT_S = 30;
            TimeSpan _c_timeout = TimeSpan.FromSeconds(TIMEOUT_S);

            string getNameString(CSteamID cstid)
                => SteamidToDiscordid.TryGetValue(cstid.m_SteamID, out ulong id) ? $"<@{id}> `{nameMaps[cstid]}`" : nameMaps[cstid];

            void pushUpdate(CSteamID cstid, string state)
            {
                var name = nameMaps[cstid];
                ScrapMechanic.Logger.LogWarning($"Player '{name}' ({cstid}) {state}");
                string discordid = getNameString(cstid);
                //_webhook.SendMessage($"@everyone {discordid} {state}");
            }

            bool updateConnectionState(CSteamID cstid, int state, int notify)
            {
                var timer = connectionTimeoutTimers[cstid];

                void alive()
                {
                    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    pushUpdate(cstid, "is now playing Scrap Mechanic!");
                }

                void dead()
                {
                    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    pushUpdate(cstid, "stopped playing Scrap Mechanic :(");
                }

                switch (state)
                {
                    // Disconnected
                    case 0:
                        if (notify == 1) return timer.Change(_c_timeout, Timeout.InfiniteTimeSpan);
                        if (notify == 2) dead();
                        break;
                    // Connecting
                    case 1:
                        if (notify != 3) return false;
                        alive();
                        break;
                    // Connected
                    case 2:
                        if (notify != 3) break;
                        alive();
                        break;
                }

                connectionStates[cstid] = state;
                return true;
            }

            void connectionDetected(CSteamID cstid, int state)
            {
                if (connectionStates.GetOrAdd(cstid, 0) == state)
                    return;

                updateConnectionState(cstid, state, connectionStates[cstid] switch { 0 => 3, 1 => 1, 2 => 2 });
            }

            DateTime lastConnectionAttempt = DateTime.Now;
            void connectionTimeout(object? _cstid) => updateConnectionState((CSteamID)_cstid!, 0, 2);

            client.OnConnectionStatusChanged += (cstid, hconn, netConnectInfo) =>
            {
                switch (netConnectInfo.m_eState) 
                {
                    // fully established connection detected
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        connectionDetected(cstid, 2);
                        break;
                    // Connection initialization detected
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                        if (!connectionTimeoutTimers.ContainsKey(cstid))
                        {
                            connectionTimeoutTimers[cstid] = new Timer(connectionTimeout, cstid, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                            Logger.LogInfo($"Now tracking: {getNameString(cstid)}");
                            //_webhook.SendMessage($"Now tracking: {getNameString(cstid)}");
                        }
                        else if (DateTime.Now - lastConnectionAttempt < _c_timeout)
                        {
                            lastConnectionAttempt = DateTime.Now;
                            connectionDetected(cstid, 1);
                        }
                        break;
                    // Connection status alive detected
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute:
                        connectionDetected(cstid, 1);
                        break;
                    default:
                        // Connection status is dropped. Start timeout timer
                        if (!netConnectInfo.IsConnectionAlive())
                        {
                            var timer = connectionTimeoutTimers
                                .GetOrAdd(cstid, _cstid => new Timer(connectionTimeout, _cstid, _c_timeout, Timeout.InfiniteTimeSpan));

                            connectionDetected(cstid, 0);
                            client.ConnectToUserAsync(cstid);
                        }
                        break;
                }
            };

            foreach (var cstid in cstids)
            {
                nameMaps[cstid] = GetName(cstid);
                client.ConnectToUserAsync(cstid);
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
