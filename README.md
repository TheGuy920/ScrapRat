# Welcome to the CrashBot repo!

This repository implements the follow CWE's:

[CWE-208](https://cwe.mitre.org/data/definitions/208.html) Comming Soon :tm: under [SkeletonKey](https://commingsoon)

[CWE-306](https://cwe.mitre.org/data/definitions/306.html) under [Player-GetConnection#L74-L76](https://github.com/TheGuy920/Crashbot/blob/main/ScrapRat/Player/Player.cs#L74-L76)
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
[CWE-307](https://cwe.mitre.org/data/definitions/307.html) Comming Soon :tm: under [SkeletonKey](https://commingsoon)

[CWE-476](https://cwe.mitre.org/data/definitions/476.html) Comming Soon :tm: under [Blacklist](https://commingsoon)
