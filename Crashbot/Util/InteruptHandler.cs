using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        }

        /// <summary>
        /// 
        /// </summary>
        public void Reset() =>
            this.source.Cancel();

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
            this.source = new();
        }
    }
}
