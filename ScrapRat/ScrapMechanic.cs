using ScrapRat.PlayerModels;
using ScrapRat.PlayerModels.Blacklist;
using Steamworks;
using System.Collections.Concurrent;
using static ScrapRat.ScrapMechanic;

namespace ScrapRat
{
    public static class ScrapMechanic
    {
        private static ConcurrentDictionary<ulong, Player> PlayerDictionary { get; } = []; 

        private static Player GetOrAddPlayer(ulong steamid) =>
            ScrapMechanic.PlayerDictionary.GetOrAdd(steamid, id => new Player(id));

        public static void Initialize()
        {
            if (SteamAPI.Init())
            {
                Console.Clear();
                Console.WriteLine("Steam API initialized.");
            }
            else
            {
                Console.WriteLine("Steam API failed to initialize.");
            }
        }

        public static class BigBrother
        {
            private static ConcurrentDictionary<ulong, MagnifiedMechanic> SpyDictionary { get; } = [];

            /// <summary>
            /// 
            /// </summary>
            /// <param name="steamid"></param>
            /// <returns></returns>
            public static MagnifiedMechanic SpyOnMechanic(ulong steamid)
            {
                if (SpyDictionary.TryGetValue(steamid, out MagnifiedMechanic target))
                {
                    return target;
                }
                else
                {
                    var player = ScrapMechanic.GetOrAddPlayer(steamid);
                    var spy = new MagnifiedMechanic(player);
                    SpyDictionary.TryAdd(steamid, spy);
                    return spy;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="steamid"></param>
            public static void SafeUntargetPlayer(CSteamID steamid) => SafeUntargetPlayer(steamid.m_SteamID);

            /// <summary>
            /// 
            /// </summary>
            /// <param name="steamid"></param>
            public static void SafeUntargetPlayer(ulong steamid)
            {
                if (SpyDictionary.TryRemove(steamid, out MagnifiedMechanic target))
                {
                    Task.Run(target.DisposeAsync).Wait();
                }
            }
        }

        public static class Blacklist
        {
            private static ConcurrentDictionary<ulong, MechanicNoMore> BlacklistDictionary { get; } = [];

            /// <summary>
            /// 
            /// </summary>
            /// <param name="steamid"></param>
            /// <param name="hideLogs"></param>
            /// <param name="blacklistAnyHost"></param>
            /// <returns></returns>
            public static MechanicNoMore Add(ulong steamid, bool hideLogs = false, bool blacklistAnyHost = false)
            {
                if (BlacklistDictionary.TryGetValue(steamid, out MechanicNoMore target))
                {
                    return target;
                }
                else
                {
                    var player = ScrapMechanic.GetOrAddPlayer(steamid);
                    var blacklisted = new MechanicNoMore(player, hideLogs, blacklistAnyHost);
                    BlacklistDictionary.TryAdd(steamid, blacklisted);
                    return blacklisted;
                }
            }


            public static void Remove(ulong steamid)
            {
                
            }


            public static void Kick(ulong steamid)
            {

            }
        }

        public static class SkeletonKey
        {
            
        }
    }
}
