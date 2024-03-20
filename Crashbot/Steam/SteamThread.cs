using Crashbot.Util;
using Steamworks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Crashbot.Steam
{
    internal class SteamFunction(Delegate action, object[] @params)
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

    internal delegate bool SteamCallback<_>(_ result) where _ : struct;
    internal delegate bool SteamCallback<_,T>(_ result, T p0) where _ : struct;
    internal delegate bool SteamCallback<_,T,G>(_ result, T p0, G p1) where _ : struct;
    internal delegate bool SteamCallback<_,T,G,O>(_ result, T p0, G p1, O p2) where _ : struct;

    internal class SteamCallback(Delegate callback, object[] @params)
    { 
        private readonly Delegate steamCallback = callback;

        public bool Invoke<T>(T result)
            => steamCallback.DynamicInvoke([result, ..@params]) as bool? ?? false;
    }

    internal class CallbackId<_>(Guid id) where _ : struct
    {
        public Guid Id => id;
    }

    internal class SteamThread : IDisposable
    {
        private readonly Thread? workerThread;
        private readonly ConcurrentQueue<SteamFunction> actionQueue = new();
        private readonly ConcurrentDictionary<object, ConcurrentDictionary<Guid, SteamCallback>> callbackQueue = new();
        private readonly AutoResetEvent actionEvent = new(false);
        private readonly InterfaceMode mode;
        private bool running = true;
        private bool steamReady = false;

        public bool SteamIsReady => steamReady;

        public SteamThread(InterfaceMode mode)
        {
            this.mode = mode;

            if (mode == InterfaceMode.Asyncronous)
            {
                this.workerThread = new Thread(this.SteamSDKThread) { Priority = ThreadPriority.Highest };
                this.workerThread.Start();
            }
        }

        public CallbackId<T> RegisterCallbackOnce<T>(SteamCallback<T> callback) where T : struct
            => this.IRegisterCallbackOnce<T>(callback, []);

        public CallbackId<T> RegisterCallbackOnce<T,G>(SteamCallback<T,G> callback, G @param0) where T : struct
            => this.IRegisterCallbackOnce<T>(callback, [@param0]);

        public CallbackId<T> RegisterCallbackOnce<T,G,O>(SteamCallback<T,G,O> callback, G @param0, O @param1) where T : struct
            => this.IRegisterCallbackOnce<T>(callback, [@param0, @param1]);

        public CallbackId<T> RegisterCallbackOnce<T,G,O,X>(SteamCallback<T,G,O,X> callback, G @param0, O @param1, X @param2) where T : struct
            => this.IRegisterCallbackOnce<T>(callback, [@param0, @param1, @param2]);

        private CallbackId<T> IRegisterCallbackOnce<T>(Delegate callback, params object[] @params) where T : struct
        {
            Guid id = Guid.NewGuid();
            if (this.callbackQueue.TryGetValue(typeof(T), out var bag)) 
            {
                bag.TryAdd(id, new SteamCallback(callback, @params));
            }
            else
            {
                var dick = new ConcurrentDictionary<Guid, SteamCallback>();
                dick.TryAdd(id, new SteamCallback(callback, @params));
                this.callbackQueue.TryAdd(typeof(T), dick);
            }

            return new(id);
        }

        public bool UnregisterCallback<T>(CallbackId<T> cbid) where T : struct =>
            this.callbackQueue.TryGetValue(typeof(T), out var bag) && bag.TryRemove(cbid.Id, out _);

        public bool ForceCallOnce<T>(CallbackId<T> cbid, T result) where T : struct =>
            this.callbackQueue.TryGetValue(typeof(T), out var bag)
            && bag.TryGetValue(cbid.Id, out var cllBk)
            && (cllBk.Invoke(result) || bag.TryRemove(cbid.Id, out _));

        public (HSteamNetConnection? Connection, SteamNetConnectionInfo_t? Info) ConnectP2P
            (ref SteamNetworkingIdentity ir, int p, int no, SteamNetworkingConfigValue_t[] op, CancellationToken? cancel = null)
        {
            ConcurrentBag<(SteamNetworkingIdentity, HSteamNetConnection, SteamNetConnectionInfo_t)> ret = [];
            AutoResetEvent connected = new(false);

            this.QueueAction(new((SteamNetworkingIdentity lir) =>
            {
            Start:
                SteamNetworkingIdentity _ir = lir;

                Logger.WriteLine($"Connecting to {_ir.GetSteamID64()}", Verbosity.Debug);
                var conn = SteamNetworkingSockets.ConnectP2P(ref _ir, p, no, op);

                SteamAPI.RunCallbacks();
                Logger.WriteLine($"Connection Loading...", Verbosity.Debug);
                SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info);

                Stopwatch sw = new();
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

                    if ((info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute)
                        && !sw.IsRunning) sw.Start();

                    if (sw.ElapsedMilliseconds > 2000)
                    {
                        Logger.WriteLine($"Connection to {_ir.GetSteamID64()} timed out", Verbosity.Minimal);
                        SteamNetworkingSockets.CloseConnection(conn, 0, "Cancelled", false);
                        SteamAPI.RunCallbacks();
                        goto Start;
                    }
                }

                ret.Add((_ir, conn, info));
                connected.Set();
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
            => GetResult(action, @params, timeout);

        public dynamic? Get(Delegate action, params object[] @params)
            => GetResult(action, @params);

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
            switch (this.mode)
            {
                case InterfaceMode.Asyncronous:
                    this.actionQueue.Enqueue(action);
                    this.actionEvent.Set();
                    break;
                case InterfaceMode.Syncronous:
                    RunAction(action);
                    break;
            }
        }

        private void SteamSDKThread()
        {
            if (!SteamAPI.Init())
            {
                Logger.Clear();
                Logger.WriteLine($"SteamAPI.Init() failed!", Verbosity.Minimal);
                return;
            }
            else
            {
                Logger.Clear();
                Logger.WriteLine($"BLoggedOn: [" + SteamUser.BLoggedOn() + "]", Verbosity.Minimal);
                this.steamReady = true;
            }

            Callback<FriendRichPresenceUpdate_t>.Create(result =>
            {
                if (!this.callbackQueue.TryGetValue(typeof(FriendRichPresenceUpdate_t), out var bag))
                    return;

                foreach (var kvp in bag.ToArray())
                    if (kvp.Value.Invoke(result))
                        bag.TryRemove(kvp);
            });

            while (this.running)
            {
                SteamAPI.RunCallbacks();
                if (!this.actionEvent.WaitOne(50) || this.actionQueue.IsEmpty)
                    continue;
                
                while (this.actionQueue.TryDequeue(out SteamFunction? action))
                    SteamThread.RunAction(action);
            }
        }

        private static void RunAction(SteamFunction action)
        {
            action.Invoke();
            SteamAPI.RunCallbacks();
        }

        public void Dispose()
        {
            this.running = false;
            this.actionEvent.Set();
            this.workerThread?.Join();
            this.actionEvent.Dispose();
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
