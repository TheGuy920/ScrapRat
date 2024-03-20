using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Crashbot.Util
{
    public class InteruptHandler
    {
        private CancellationTokenSource source = new();

        /// <summary>
        /// 
        /// </summary>
        public bool WasInterupted => this.source.Token.IsCancellationRequested;

        /// <summary>
        /// 
        /// </summary>
        public CancellationToken Token => this.source.Token;

        /// <summary>
        /// 
        /// </summary>
        public void Interupt() =>
            this.source.Cancel();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timout"></param>
        public void Interupt(int timout) =>
            this.Interupt(TimeSpan.FromMilliseconds(timout));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timout"></param>
        public void Interupt(TimeSpan timout)
        {
            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne(timout);
            if (Interlocked.Read(ref this.refs) > 0)
                this.refsCompleted.WaitOne(timout);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Reset()
        {
            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne();
            if (Interlocked.Read(ref this.refs) > 0)
                this.refsCompleted.WaitOne();
            this.source = new();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timout"></param>
        public void Reset(int timout) =>
            this.Reset(TimeSpan.FromMilliseconds(timout));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timout"></param>
        public void Reset(TimeSpan timout)
        {
            this.source.Cancel();
            this.source.Token.WaitHandle.WaitOne(timout);
            if (Interlocked.Read(ref this.refs) > 0)
                this.refsCompleted.WaitOne(timout);
            this.source = new();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="delegate"></param>
        /// <param name="params"></param>
        public void ExecuteAfter(TimeSpan delay, Delegate @delegate, params object[] @params)
        {
            Task.Delay(delay).ContinueWith(_ => @delegate.DynamicInvoke(@params));
            this.Interupt();
        }

        private readonly AutoResetEvent refsCompleted = new(false);
        private long refs = 0;

        /// <summary>
        /// 
        /// </summary>
        public void Register() => Interlocked.Increment(ref this.refs);


        /// <summary>
        /// 
        /// </summary>
        public void Completed()
        {
            if (Interlocked.Decrement(ref this.refs) <= 0)
                this.refsCompleted.Set();
        }
    }
}
