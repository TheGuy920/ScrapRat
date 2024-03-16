using SteamKit2;
using SteamKit2.Authentication;
using System.Diagnostics;

namespace Crashbot
{
    internal class SteamSocket
    {
        public class Credentials
        {
            public bool LoggedOn { get; set; }
            public ulong SessionToken { get; set; }

            public bool IsValid
            {
                get { return LoggedOn; }
            }
        }

        public SteamClient steamClient;
        public SteamUser steamUser;
        public SteamContent steamContent;
        public readonly SteamApps steamApps;
        private readonly SteamCloud steamCloud;

        public Dictionary<string, string> LoginTokens { get; private set; }

        public readonly CallbackManager callbacks;

        private readonly bool authenticatedUser;
        private bool bConnected;
        private bool bConnecting;
        private bool bAborted;
        private bool bExpectingDisconnectRemote;
        private bool bDidDisconnect;
        private bool bIsConnectionRecovery;
        private int connectionBackoff;
        private int seq; // more hack fixes
        private DateTime connectTime;
        private AuthSession? authSession;

        // input
        private readonly SteamUser.LogOnDetails logonDetails;
        // output
        private readonly Credentials credentials;

        private static readonly TimeSpan STEAM3_TIMEOUT = TimeSpan.FromSeconds(30);

        public delegate void OnPICSChanged(SteamApps.PICSChangesCallback cb);
        public event OnPICSChanged? OnPICSChanges;

        public delegate void OnClientLogin(SteamUser.LoggedOnCallback logon);
        public event OnClientLogin? OnClientsLogin;

        public delegate void OnClientDisconnect(SteamClient.DisconnectedCallback disconnect);
        public event OnClientDisconnect? OnClientsDisconnect;

        public delegate void FailedToReconnect();
        public event FailedToReconnect? OnFailedToReconnect;

        public SteamSocket(SteamUser.LogOnDetails details)
        {
            this.logonDetails = details;
            this.authenticatedUser = details.Username != null;
            this.credentials = new Credentials();
            this.bConnected = false;
            this.bConnecting = false;
            this.bAborted = false;
            this.bExpectingDisconnectRemote = false;
            this.bDidDisconnect = false;
            this.seq = 0;

            var clientConfiguration = SteamConfiguration.Create(config => config.WithHttpClientFactory(HttpClientFactory.CreateHttpClient));
            this.steamClient = new SteamClient(clientConfiguration);

            this.steamUser = this.steamClient.GetHandler<SteamUser>()!;
            this.steamApps = this.steamClient.GetHandler<SteamApps>()!;
            this.steamCloud = this.steamClient.GetHandler<SteamCloud>()!;
            var steamUnifiedMessages = this.steamClient.GetHandler<SteamUnifiedMessages>()!;
            this.steamContent = this.steamClient.GetHandler<SteamContent>()!;

            this.callbacks = new CallbackManager(this.steamClient);
            this.SubscribeAll();

            this.LoginTokens = [];

            File.WriteAllText("steam_appid.txt", "387990");
        }

        private void SubscribeAll()
        {
            this.callbacks.Subscribe<SteamClient.ConnectedCallback>(ConnectedCallback);
            this.callbacks.Subscribe<SteamClient.DisconnectedCallback>(DisconnectedCallback);
            this.callbacks.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);
            this.callbacks.Subscribe<SteamUser.SessionTokenCallback>(SessionTokenCallback);
        }

        private delegate bool WaitCondition();

        private readonly object steamLock = new();

        private bool WaitUntilCallback(Action submitter, WaitCondition waiter)
        {
            while (!this.bAborted && !waiter())
            {
                lock (this.steamLock)
                {
                    submitter();
                }

                var seq = this.seq;
                do
                {
                    lock (this.steamLock)
                    {
                        this.WaitForCallbacks();
                    }
                } while (!this.bAborted && this.seq == seq && !waiter());
            }

            return this.bAborted;
        }

        private void ResetConnectionFlags()
        {
            this.bExpectingDisconnectRemote = false;
            this.bDidDisconnect = false;
            this.bIsConnectionRecovery = false;
        }

        public void Connect()
        {
            if (!Steamworks.SteamAPI.Init())
            {
                Debug.WriteLine("SteamAPI.Init() failed!");
                return;
            }
            Console.WriteLine($"[{this.GetType().FullName}]: Connecting to Steam3...");

            this.bAborted = false;
            this.bConnected = false;
            this.bConnecting = true;
            this.connectionBackoff = 0;
            this.authSession = null;

            this.ResetConnectionFlags();

            this.connectTime = DateTime.Now;
            this.steamClient.Connect();
        }

        private void Abort(bool sendLogOff = true)
        {
            this.OnFailedToReconnect?.Invoke();
            this.Disconnect(sendLogOff);
        }

        public void Disconnect(bool sendLogOff = true)
        {
            if (sendLogOff)
            {
                this.steamUser.LogOff();
            }

            this.bAborted = true;
            this.bConnected = false;
            this.bConnecting = false;
            this.bIsConnectionRecovery = false;
            this.steamClient.Disconnect();

            // flush callbacks until our disconnected event
            while (!this.bDidDisconnect)
            {
                this.callbacks.RunWaitAllCallbacks(TimeSpan.FromMilliseconds(100));
            }
        }

        public void Reconnect()
        {
            this.bIsConnectionRecovery = true;
            this.Connect();
        }

        private void WaitForCallbacks()
        {
            this.callbacks.RunWaitCallbacks(TimeSpan.FromSeconds(1));

            var diff = DateTime.Now - this.connectTime;

            if (diff > STEAM3_TIMEOUT && !this.bConnected)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Timeout connecting to Steam3.");
                this.OnFailedToReconnect?.Invoke();
                Abort();
            }
        }

        private async void ConnectedCallback(SteamClient.ConnectedCallback connected)
        {
            this.bConnecting = false;
            this.bConnected = true;

            // Update our tracking so that we don't time out, even if we need to reconnect multiple times,
            // e.g. if the authentication phase takes a while and therefore multiple connections.
            this.connectTime = DateTime.Now;
            this.connectionBackoff = 0;

            if (!this.authenticatedUser)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Logging anonymously into Steam3...");
                this.steamUser.LogOnAnonymous();
            }
            else
            {
                if (this.logonDetails.Username != null)
                {
                    Console.WriteLine($"[{this.GetType().FullName}]: Logging '{this.logonDetails.Username}' into Steam3...");
                }

                if (this.authSession is null)
                {
                    if (this.logonDetails.Username != null && this.logonDetails.Password != null && this.logonDetails.AccessToken is null)
                    {
                        try
                        {
                            this.authSession = await this.steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                            {
                                Username = logonDetails.Username,
                                Password = logonDetails.Password,
                                IsPersistentSession = true
                            });
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[{this.GetType().FullName}]: Failed to authenticate with Steam: " + ex.Message);
                            Console.Error.WriteLine(ex);
                            this.Abort(false);
                            return;
                        }
                    }
                }

                if (this.authSession != null)
                {
                    try
                    {
                        var result = await authSession.PollingWaitForResultAsync();

                        this.logonDetails.Username = result.AccountName;
                        this.logonDetails.Password = null;
                        this.logonDetails.AccessToken = result.RefreshToken;

                        this.LoginTokens[result.AccountName] = result.RefreshToken;
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[{this.GetType().FullName}]: Failed to authenticate with Steam: " + ex.ToString());
                        this.OnFailedToReconnect?.Invoke();
                        this.Abort(false);
                        return;
                    }

                    this.authSession = null;
                }

                this.steamUser.LogOn(this.logonDetails);
            }
        }

        private void DisconnectedCallback(SteamClient.DisconnectedCallback disconnected)
        {
            this.bDidDisconnect = true;

            Console.WriteLine(
                 $"[{this.GetType().FullName}]: Disconnected: bIsConnectionRecovery = {this.bIsConnectionRecovery}, UserInitiated = {disconnected.UserInitiated}, bExpectingDisconnectRemote = {this.bExpectingDisconnectRemote}");

            // When recovering the connection, we want to reconnect even if the remote disconnects us
            if (!this.bIsConnectionRecovery && (disconnected.UserInitiated || this.bExpectingDisconnectRemote))
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Disconnected from Steam");

                // Any operations outstanding need to be aborted
                this.bAborted = true;

                this.OnClientsDisconnect?.Invoke(disconnected);
                this.OnFailedToReconnect?.Invoke();
            }
            else if (this.connectionBackoff >= 10)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Could not connect to Steam after 10 tries");
                this.OnFailedToReconnect?.Invoke();
                this.Abort(false);
            }
            else if (!this.bAborted)
            {
                if (this.bConnecting)
                {
                    Console.WriteLine($"[{this.GetType().FullName}]: Connection to Steam failed. Trying again");
                }
                else
                {
                    Console.WriteLine($"[{this.GetType().FullName}]: Lost connection to Steam. Reconnecting");
                }

                Thread.Sleep(1000 * ++connectionBackoff);

                // Any connection related flags need to be reset here to match the state after Connect
                this.ResetConnectionFlags();
                this.steamClient.Connect();
            }
        }

        private void LogOnCallback(SteamUser.LoggedOnCallback loggedOn)
        {
            var isSteamGuard = loggedOn.Result == EResult.AccountLogonDenied;
            var is2FA = loggedOn.Result == EResult.AccountLoginDeniedNeedTwoFactor;
            var isAccessToken = true && this.logonDetails.AccessToken != null && loggedOn.Result == EResult.InvalidPassword; // TODO: Get EResult for bad access token

            if (isSteamGuard || is2FA || isAccessToken)
            {
                this.bExpectingDisconnectRemote = true;
                this.Abort(false);

                if (!isAccessToken)
                {
                    Console.WriteLine($"[{this.GetType().FullName}]: This account is protected by Steam Guard.");
                }

                if (is2FA)
                {
                    do
                    {
                        Console.WriteLine($"[{this.GetType().FullName}]: Please enter your 2 factor auth code from your authenticator app");
                        break;
                        //this.logonDetails.TwoFactorCode = Console.ReadLine();
                    } while (string.Empty == this.logonDetails.TwoFactorCode);
                }
                else if (isAccessToken)
                {
                    this.LoginTokens.Remove(this.logonDetails.Username!);
                    //this.settings.Save();

                    // TODO: Handle gracefully by falling back to password prompt?
                    Console.WriteLine($"[{this.GetType().FullName}]: Access token was rejected.");
                    this.OnFailedToReconnect?.Invoke();
                    this.Abort(false);
                    return;
                }
                else
                {
                    do
                    {
                        Console.WriteLine($"[{this.GetType().FullName}]: Please enter the authentication code sent to your email address: ");
                        //this.logonDetails.AuthCode = Console.ReadLine();
                        break;
                    } while (string.Empty == this.logonDetails.AuthCode);
                }

                Console.WriteLine($"[{this.GetType().FullName}]: Retrying Steam3 connection...");
                this.Connect();

                return;
            }

            if (loggedOn.Result == EResult.TryAnotherCM)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Retrying Steam3 connection (TryAnotherCM)...");

                this.Reconnect();

                return;
            }

            if (loggedOn.Result == EResult.ServiceUnavailable)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Unable to login to Steam3: {loggedOn.Result}");
                this.OnFailedToReconnect?.Invoke();
                this.Abort(false);

                return;
            }

            if (loggedOn.Result != EResult.OK)
            {
                Console.WriteLine($"[{this.GetType().FullName}]: Unable to login to Steam3: {loggedOn.Result}");
                this.OnFailedToReconnect?.Invoke();
                this.Abort();

                return;
            }

            Console.WriteLine($"[{this.GetType().FullName}]: Logged In");

            this.OnClientsLogin?.Invoke(loggedOn);

            this.seq++;
            this.credentials.LoggedOn = true;
        }

        private void SessionTokenCallback(SteamUser.SessionTokenCallback sessionToken)
        {
            this.credentials.SessionToken = sessionToken.SessionToken;
        }
    }
}
