using ScrapMechanic.Networking;
using Steamworks;

namespace ScrapMechanic.Util
{
    public static class Extensions
    {
        public static bool IsConnectionAlive(this SteamNetConnectionInfo_t info) => info.m_eState switch
        {
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting => true,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute => true,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected => true,
            _ => false
        };

        public static ConnectionState ToConnectionState(this ESteamNetworkingConnectionState state) => state switch
        {
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute => ConnectionState.Connecting,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting => ConnectionState.Connecting,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected => ConnectionState.Connected,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer => ConnectionState.Disconnected,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally => ConnectionState.Disconnected,
            _ => ConnectionState.None
        };
    }
}
