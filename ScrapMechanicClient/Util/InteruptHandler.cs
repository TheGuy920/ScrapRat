﻿using System.Collections.Concurrent;
using System.Reflection;

namespace ScrapMechanic.Util
{
    internal class InteruptHandler : IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<object, byte> refs = [];
        private readonly ManualResetEvent refsCompleted = new(false);
        private CancellationTokenSource source = new();
        private readonly object lockObject = new();
        private volatile bool IsCanceling = false;

        public InteruptHandler()
        {
            this.source.Token.Register(this.Interupt);
        }

        /// <summary>
        /// Cancelation token
        /// </summary>
        public CancellationToken Token => this.source.Token;

        /// <summary>
        /// Interupt the current operation async
        /// </summary>
        public void InteruptAsync() =>
            this.Interupt(TimeSpan.Zero);

        /// <summary>
        /// Interupt the current operation
        /// </summary>
        public void Interupt() =>
            this.Interupt(Timeout.Infinite);

        /// <summary>
        /// Interupt the current operation
        /// </summary>
        /// <param name="timout"></param>
        public void Interupt(int timout) =>
            this.Interupt(TimeSpan.FromMilliseconds(timout));

        /// <summary>
        /// Interupt the current operation
        /// </summary>
        /// <param name="timout"></param>
        public void Interupt(TimeSpan timout)
        {
            if (this.CheckLock() || this.source.IsCancellationRequested)
                return;

            bool needsReset = this.refs.Count > 0;

            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne(timout);

            if (needsReset) this.refsCompleted.WaitOne(timout);
            this.ResetLock();
        }

        /// <summary>
        /// Cancels and resets the interupt handler
        /// </summary>
        public void Reset()
        {
            if (this.CheckLock())
                return;

            bool needsReset = this.refs.Count > 0;

            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne();

            if (needsReset) this.refsCompleted.WaitOne();
            this.NewToken();
        }

        /// <summary>
        /// Cancels and resets the interupt handler
        /// </summary>
        /// <param name="timout"></param>
        public void Reset(int timout) =>
            this.Reset(TimeSpan.FromMilliseconds(timout));

        /// <summary>
        /// Cancels and resets the interupt handler
        /// </summary>
        /// <param name="timout"></param>
        public void Reset(TimeSpan timout)
        {
            if (this.CheckLock())
                return;

            bool needsReset = this.refs.Count > 0;

            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne(timout);

            if (needsReset) this.refsCompleted.WaitOne(timout);
            this.NewToken();
        }

        /// <summary>
        /// Runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="action"></param>
        public dynamic? RunCancelable<T>(Func<T> @action) where T : notnull =>
            this.RunCancelable(@action, []);

        /// <summary>
        /// Runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="action"></param>
        public dynamic? RunCancelable(Action @action) =>
            this.RunCancelable(@action, []);

        /// <summary>
        /// Runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public dynamic? RunCancelable<T>(Action<T> function, T p0) where T : notnull =>
            this.RunCancelable(function, [p0]);

        /// <summary>
        /// Runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public dynamic? RunCancelable<T, G>(Action<T, G> function, T p0, G p1) where T : notnull where G : notnull =>
            this.RunCancelable(function, [p0, p1]);

        /// <summary>
        /// Runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public dynamic? RunCancelable<T, G, O>(Action<T, G, O> function, T p0, G p1, O p2) where T : notnull where G : notnull where O : notnull =>
            this.RunCancelable(function, [p0, p1, p2]);

        /// <summary>
        /// Runs a delegate (async) with the ability to cancel it
        /// </summary>
        /// <param name="delegate"></param>
        /// <param name="params"></param>
        public dynamic? RunCancelable(Delegate @delegate, params object[] @params)
        {
            this.refs.TryAdd(@delegate, 0);

            return Task.Run(() =>
            {
                try
                {
                    return @delegate.DynamicInvoke(@params);
                }
                catch (OperationCanceledException) { return null; }
                catch (TargetInvocationException e) 
                {
                    if (e.InnerException is OperationCanceledException)
                        return null;
                    throw;
                }
            }, this.Token)
            .ContinueWith(t => { this.refs.TryRemove(@delegate, out _); return t.Result; })
            .ContinueWith(t => { this.ExecutionCompleted(@delegate); return t.Result; })
            .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="action"></param>
        public void RunCancelableAsync(Action @action) =>
            this.RunCancelableAsync(@action, []);

        /// <summary>
        /// Async runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public void RunCancelableAsync<T>(Action<T> function, T p0) where T : notnull =>
            this.RunCancelableAsync(function, [p0]);

        /// <summary>
        /// Async runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public void RunCancelableAsync<T, G>(Action<T, G> function, T p0, G p1) where T : notnull where G : notnull =>
            this.RunCancelableAsync(function, [p0, p1]);

        /// <summary>
        /// Async runs an action (async) with the ability to cancel it
        /// </summary>
        /// <param name="function"></param>
        public void RunCancelableAsync<T, G, O>(Action<T, G, O> function, T p0, G p1, O p2) where T : notnull where G : notnull where O : notnull =>
            this.RunCancelableAsync(function, [p0, p1, p2]);

        /// <summary>
        /// Async runs a delegate (async) with the ability to cancel it
        /// </summary>
        /// <param name="delegate"></param>
        /// <param name="params"></param>
        public void RunCancelableAsync(Delegate @delegate, params object[] @params)
        {
            this.refs.TryAdd(@delegate, 0);

            Task.Run(() =>
            {
                try
                {
                    @delegate.DynamicInvoke(@params);
                }
                catch (OperationCanceledException) { return; }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException is OperationCanceledException)
                        return;
                    throw;
                }
            }, this.Token)
            .ContinueWith(__ => this.refs.TryRemove(@delegate, out _))
            .ContinueWith(_ => this.ExecutionCompleted(@delegate));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        public void RunOnCancel(Action action) =>
            this.Token.Register(action);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delegate"></param>
        /// <param name="params"></param>
        public void RunOnCancel(Delegate @delegate, params object[] @params) =>
            this.Token.Register(() => @delegate.DynamicInvoke(@params));

        private void ExecutionCompleted(Task<Delegate> completedTask) =>
            this.ExecutionCompleted(completedTask.Result);

        private void ExecutionCompleted(Delegate @delegate)
        {
            this.refs.TryRemove(@delegate, out _);
            if (this.refs.IsEmpty) this.refsCompleted.Set();
        }

        /// <summary>
        /// True if the interupt handler is currently canceling
        /// </summary>
        /// <returns></returns>
        private bool CheckLock()
        {
            lock (this.lockObject)
            {
                if (this.IsCanceling)
                {
                    return true;
                }
                this.IsCanceling = true;
                return false;
            }
        }

        private void ResetLock()
        {
            this.IsCanceling = false;
        }

        /// <summary>
        /// Unsafe token reset
        /// </summary>
        private void NewToken()
        {
            this.source = new();
            this.refs.Clear();
            this.refsCompleted.Reset();

            this.source.Token.Register(this.Interupt);
            this.ResetLock();
        }

        /// <summary>
        /// Disposes of the interupt handler
        /// </summary>
        public void Dispose()
        {
            this.refs.Clear();
            this.source.CancelAsync();
            this.refsCompleted.Set();

            this.refsCompleted.Dispose();
            this.source.Dispose();
        }

        /// <summary>
        /// Disposes of the interupt handler safely by waiting for all handles to complete
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ValueTask DisposeAsync()
        {
            this.source.Cancel();
            this.refsCompleted.WaitOne();
            this.refs.Clear();
            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
