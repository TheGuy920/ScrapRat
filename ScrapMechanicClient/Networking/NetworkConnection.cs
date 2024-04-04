using ScrapMechanic.Util;
using Steamworks;
using System.Runtime.InteropServices;

namespace ScrapMechanic.Networking
{
    internal class NetworkConnection
    {
        private HSteamNetConnection SteamNetConnection;
        private SteamNetworkingIdentity Identity;
        private SteamNetConnectionInfo_t ConnectionInfo;

        private readonly object _statusLock = new();
        private ESteamNetworkingConnectionState _status;
        public ESteamNetworkingConnectionState Status 
        { 
            get => _status; 
            private set
            {
                lock (_statusLock)
                {
                    if (_status == value)
                        return;
                    
                    _status = value;
                    OnConnectionStatusChanged?.Invoke(Identity.GetSteamID(), SteamNetConnection, ConnectionInfo);

                    if (_status == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                        this.ListenForMessages();
                    else
                        this.MessageInterupt.Reset();
                }
            }
        }

        public event ConnectionStatusChanged? OnConnectionStatusChanged;
        public event ConnectionMessageReceived? OnConnectionMessageReceived;

        private readonly Thread StatusThread;
        private readonly InteruptHandler MessageInterupt;
        private readonly CancellationTokenSource CancelToken;
        public NetworkConnection(CSteamID cSteamID)
        {
            Identity = new SteamNetworkingIdentity();
            Identity.SetSteamID(cSteamID);
            StatusThread = new(UpdateStatus);
            CancelToken = new();
            MessageInterupt = new();
            StatusThread.Start();
        }

        ~NetworkConnection()
        {
            MessageInterupt.Interupt();
            CancelToken.Cancel();
            StatusThread.Join();
        }

        private async void UpdateStatus()
        {
            while (!CancelToken.IsCancellationRequested)
            {
                if (SteamNetworkingSockets.GetConnectionInfo(SteamNetConnection, out ConnectionInfo))
                    Status = ConnectionInfo.m_eState;

                await Task.Delay(25, CancelToken.Token);
            }
        }

        private void ListenForMessages()
        {
            this.MessageInterupt.Reset();
            this.MessageInterupt.RunCancelableAsync(async (CancellationToken token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    IntPtr[] messagePtrArray = new IntPtr[1];
                    int messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(SteamNetConnection, messagePtrArray, 1);

                    if (messageCount == 0)
                    {
                        continue;
                    }

                    if (messageCount < 0)
                    {
                        Logger.LogError("Error checking for messages.");
                        continue;
                    }

                    if (messagePtrArray == null)
                    {
                        Logger.LogError("Error: null messagePtrArray");
                        continue;
                    }

                    for (int i = 0; i < messageCount; i++)
                    {
                        IntPtr msgIntPtr = messagePtrArray[i];
                        if (msgIntPtr != IntPtr.Zero)
                        {
                            SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgIntPtr);
                            // Process the message (netMessage.m_pData, netMessage.m_cbSize)
                            this.OnConnectionMessageReceived?.Invoke(Identity.GetSteamID(), SteamNetConnection, netMessage);
                            // Release the message when done
                            SteamNetworkingMessage_t.Release(msgIntPtr);
                        }
                    }

                    await Task.Delay(5, token);
                }
            }, MessageInterupt.Token);
        }

        /// <summary>
        /// Initializes a new <see cref="HSteamNetConnection">connection</see> to the <see cref="SteamNetworkingIdentity">remote host</see>.
        /// </summary>
        /// <param name="port">The virtual port for the connection</param>
        /// <param name="options">Optional <see cref="SteamNetworkingConfigValue_t">connection options</see></param>
        /// <param name="returnWaitHandle">return wait handle (true) or return immediately (false)</param>
        /// <returns>Optionally return a wait handle for 
        /// <see cref="SteamNetConnectionInfo_t.m_eState">State</see> == 
        /// <see cref="ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected">Connected</see></returns>
        public Task ConnectP2P(int port, SteamNetworkingConfigValue_t[] options, bool returnWaitHandle = true, bool closeOldConnection = true)
        {
            if (closeOldConnection && SteamNetworkingSockets.GetConnectionInfo(SteamNetConnection, out var info) && info.IsConnectionAlive())
                this.CloseConnection(0).Wait();

            var sid = Identity.GetSteamID();
            Identity = new();
            Identity.SetSteamID(sid);

            // Logger.LogInfo($"Connecting to {sid}...");
            SteamNetConnection = SteamNetworkingSockets.ConnectP2P(ref Identity, port, options.Length, options);

            if (!returnWaitHandle)
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                while (this.Status != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    await Task.Delay(100);
            });
        }

        /// <summary>
        /// Initiates a close <see cref="HSteamNetConnection">connection</see> request.
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="debugMsg"></param>
        /// <param name="returnWaitHandle">return wait handle (true) or return immediately (false)</param>
        /// <returns>Optionally return a wait handle for 
        /// <see cref="SteamNetConnectionInfo_t.m_eState">State</see> != 
        /// <see cref="Extensions.IsConnectionAlive(SteamNetConnectionInfo_t)">Alive</see></returns>
        public Task CloseConnection(ESteamNetConnectionEnd reason, string debugMsg = "Closed by Peer", bool returnWaitHandle = true)
        {
            if (SteamNetworkingSockets.CloseConnection(SteamNetConnection, (int)reason, debugMsg, false))
            {
                Logger.LogInfo($"Closing connection to {Identity.GetSteamID()}...");

                if (!returnWaitHandle)
                    return Task.CompletedTask;

                return Task.Run(async () =>
                {
                    while (SteamNetworkingSockets.GetConnectionInfo(SteamNetConnection, out var info))
                    {
                        if (!info.IsConnectionAlive())
                            return;

                        await Task.Delay(100);
                    }
                });
            }

            return Task.FromException(new Exception("Failed to initiate close connection."));
        }

        public static implicit operator HSteamNetConnection(NetworkConnection connection) => connection.SteamNetConnection;

        public static implicit operator SteamNetworkingIdentity(NetworkConnection connection) => connection.Identity;        
    }

    public static class Extensions
    {
        public static bool IsConnectionAlive(this SteamNetConnectionInfo_t info) => info.m_eState switch
        {
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting => true,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute => true,
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected => true,
            _ => false
        };
    }
}
