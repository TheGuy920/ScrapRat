using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapRat.PlayerModels
{
    public delegate void ObservableEventHandler(ObservableEvent @event);

    public enum ObservableEvent
    {
        NowPlaying = 0x02,
        StoppedPlaying = 0x04,
    }
}
