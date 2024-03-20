using ScrapRat.Util;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapRat
{
    public interface IPlayer : IAsyncDisposable
    {
        /// <summary>
        /// Name of the player
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Background process interupt handler. Dont touch if you dont know what you are doing.
        /// </summary>
        public InteruptHandler Interupt { get; }

        /// <summary>
        /// SteamID of the player
        /// </summary>
        public CSteamID SteamID { get; }

        /// <summary>
        /// Privacy settings of the player
        /// </summary>
        public PrivacySettings Privacy { get; }

        /// <summary>
        /// Event for when the player info is loaded. Imediately fires if the player is already loaded.
        /// </summary>
        public virtual event PlayerLoadedEventHandler? PlayerLoaded { add { } remove { } }
    }
}
