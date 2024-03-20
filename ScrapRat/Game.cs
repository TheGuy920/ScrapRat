using ScrapRat.Spy;
using Steamworks;
using System.Collections.Concurrent;

namespace ScrapRat
{
    public static class Game
    {
        private static ConcurrentDictionary<ulong, Player> PlayerDictionary { get; } = []; 

        private static Player GetOrAddPlayer(ulong steamid) =>
            Game.PlayerDictionary.GetOrAdd(steamid, id => new Player(id));

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

        public static class Spy
        {
            private static ConcurrentDictionary<ulong, SpyTarget> SpyDictionary { get; } = [];

            public static SpyTarget TargetPlayer(ulong steamid)
            {
                if (SpyDictionary.TryGetValue(steamid, out SpyTarget target))
                {
                    return target;
                }
                else
                {
                    var player = Game.GetOrAddPlayer(steamid);
                    var spy = new SpyTarget(player);
                    SpyDictionary.TryAdd(steamid, spy);
                    return spy;
                }
            }

            public static void SafeUntargetPlayer(ulong steamid)
            {
                if (SpyDictionary.TryRemove(steamid, out SpyTarget target))
                {
                    target.DisposeAsync();
                }
            }
        }

        public static class Blacklist
        {
            public static void Add(string steamid)
            {
                
            }

            public static void Remove(string steamid)
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
