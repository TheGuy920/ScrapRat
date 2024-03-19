using Steamworks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Crashbot
{
    public class SteamFunction(Delegate action, object[] @params)
    {
        public object? Invoke()
            => action.Method.Invoke(action.Target, @params);

        public static implicit operator SteamFunction(Delegate action)
            => new(action, []);

        private SteamFunction SetParams(object[] @_params)
        {
            @params = @_params;
            return this;
        }

        public static SteamFunction operator +(SteamFunction mg, object[] @_params)
            => mg.SetParams(@_params);
    }

    public class SteamThread : IDisposable
    {
        private readonly Thread? _workerThread;
        private readonly ConcurrentQueue<SteamFunction> _actionQueue = new();
        private readonly AutoResetEvent _actionEvent = new(false);
        private readonly InterfaceMode mode;
        private bool _running = true;
        private bool _steamReady = false;

        public bool SteamIsReady => this._steamReady;

        public SteamThread(InterfaceMode mode)
        {
            this.mode = mode;

            if (mode == InterfaceMode.Asyncronous)
            {
                this._workerThread = new Thread(this.SteamSDKThread) { Priority = ThreadPriority.Highest };
                this._workerThread.Start();
            }
        }

        public (HSteamNetConnection? Connection, SteamNetConnectionInfo_t? Info) ConnectP2P
            (ref SteamNetworkingIdentity ir, int p, int no, SteamNetworkingConfigValue_t[] op, CancellationToken? cancel = null)
        {
            ConcurrentBag<(SteamNetworkingIdentity, HSteamNetConnection, SteamNetConnectionInfo_t)> ret = [];
            AutoResetEvent connected = new(false);

            this.QueueAction(new((SteamNetworkingIdentity lir) =>
            {
                SteamNetworkingIdentity _ir = lir;
                var conn = SteamNetworkingSockets.ConnectP2P(ref _ir, p, no, op);
                
                SteamAPI.RunCallbacks();
                SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info);

                Stopwatch sw = Stopwatch.StartNew();
                while (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                    && info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
                {
                    SteamAPI.RunCallbacks();
                    SteamNetworkingSockets.GetConnectionInfo(conn, out info);

                    if (cancel?.CanBeCanceled == true && cancel?.IsCancellationRequested == true)
                    {
                        SteamNetworkingSockets.CloseConnection(conn, 0, "Cancelled", false);
                        SteamNetworkingSockets.ResetIdentity(ref _ir);
                        SteamAPI.RunCallbacks();
                        connected.Set();
                        return;
                    }

                    if (sw.ElapsedMilliseconds > 5000)
                    {
                        Console.WriteLine($"Connection state: {info.m_eState}", Verbosity.Debug);
                        sw.Restart();
                    }
                }

                ret.Add((_ir, conn, info));
                connected.Set();

                SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, new IntPtr[1], 1);
            }, [ir]));

            connected.WaitOne();
            if (!ret.IsEmpty)
            {
                var pack = ret.First();
                ir = pack.Item1;
                return (pack.Item2, pack.Item3);
            }
            return (null, null);
        }

        public dynamic? Get(TimeSpan timeout, Delegate action, params object[] @params)
            => this.GetResult(action, @params, timeout);

        public dynamic? Get(Delegate action, params object[] @params)
            => this.GetResult(action, @params);

        public object? GetResult(SteamFunction action, object[] @params, TimeSpan? timeout = null)
        {
            ConcurrentBag<object?> result = [];

            AutoResetEvent returnResultReady = new(false);
            SteamFunction original = action + @params;
            SteamFunction @new = new(() => 
            { 
                result.Add(original.Invoke());
                returnResultReady.Set();
            }, []);

            this.QueueAction(@new);
            
            if (timeout.HasValue)
                returnResultReady.WaitOne(timeout.Value);
            else
                returnResultReady.WaitOne();

            return result.FirstOrDefault();
        }

        public void Run(Delegate action, params object[] @params)
            => this.QueueAction(action, @params);

        private void QueueAction(SteamFunction action, object[] @params)
            => this.QueueAction(action + @params);

        private void QueueAction(SteamFunction action)
        {
            switch (mode)
            {
                case InterfaceMode.Asyncronous:
                    this._actionQueue.Enqueue(action);
                    this._actionEvent.Set();
                    break;
                case InterfaceMode.Syncronous:
                    SteamThread.RunAction(action);
                    break;
            }
        }

        private void SteamSDKThread()
        {
            if (!SteamAPI.Init())
            {
                Console.Clear();
                Console.WriteLine($"SteamAPI.Init() failed!", Verbosity.Minimal);
                return;
            }
            else
            {
                Console.Clear();
                Console.WriteLine($"BLoggedOn: [" + SteamUser.BLoggedOn() + "]", Verbosity.Minimal);
                this._steamReady = true;
            }

            while (this._running)
            {
                if (this._actionEvent.WaitOne(50) || !this._actionQueue.IsEmpty)
                {
                    while (this._actionQueue.TryDequeue(out SteamFunction? action))
                        SteamThread.RunAction(action);
                    
                    continue;
                }
                SteamAPI.RunCallbacks();
            }
        }

        private static void RunAction(SteamFunction action)
        {
            action.Invoke();
            SteamAPI.RunCallbacks();
        }

        public void Dispose()
        {
            this._running = false;
            this._actionEvent.Set();
            this._workerThread?.Join();
            this._actionEvent.Dispose();
        }

        public bool GetConnectionInfo(HSteamNetConnection conn, out SteamNetConnectionInfo_t info)
        {
            ConcurrentBag<(bool, SteamNetConnectionInfo_t)> result = [];
            AutoResetEvent returnResultReady = new(false);

            this.QueueAction(new(() =>
            {
                var res = SteamNetworkingSockets.GetConnectionInfo(conn, out var pinfo);
                result.Add((res, pinfo));
                returnResultReady.Set();
            }, []));

            returnResultReady.WaitOne();
            
            if (!result.IsEmpty)
            {
                var pack = result.FirstOrDefault();
                info = pack.Item2;
                return pack.Item1;
            }
            else
            {
                info = new SteamNetConnectionInfo_t();
                return false;
            }
        }

        public EResult SendMessageToConnection(HSteamNetConnection conn, int v1, uint v2, int v3)
        {
            ConcurrentBag<EResult> result = [];
            AutoResetEvent returnResultReady = new(false);
            this.QueueAction(new(() =>
            {
                var res = SteamNetworkingSockets.SendMessageToConnection(conn, v1, v2, v3, out long _);
                result.Add(res);
                returnResultReady.Set();
            }, []));

            returnResultReady.WaitOne();
            return result.FirstOrDefault();
        }
    }
}
