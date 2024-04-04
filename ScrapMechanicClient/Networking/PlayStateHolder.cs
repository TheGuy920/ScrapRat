using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapMechanic.Networking
{
    public enum EPlayState
    {
        Dead = 0x00,
        Playing = 0x01,
        NotPlaying = 0x02
    }

    public enum ConnectionState
    {
        None = 0x00,
        Connecting = 0x01,
        Connected = 0x02,
        Disconnected = 0x03
    }

    internal record PlayStateHolder
    {
        public Timer? ConnectionTimeoutTimer { get; set; }
        public DateTime LastTimeAlive { get; set; } = DateTime.MinValue;
        public EPlayState CurrentPlayState { get; set; } = EPlayState.Dead;
        public ConnectionState LastConnectionState { get; set; } = ConnectionState.None;
        public ESteamNetworkingConnectionState LastSteamConnectionState { get; set; } 
            = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None;
    }
}
