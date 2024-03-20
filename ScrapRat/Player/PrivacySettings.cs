using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapRat
{
    public enum PrivacySettings
    {
        None = -0x01,
        Private = 0x01,
        FriendsOnly = 0x02,
        Public = 0x03
    }

    public static class PrivacySettingsExtensions
    {
        public static string ToFriendlyString(this PrivacySettings privacySettings)
        {
            return privacySettings switch
            {
                PrivacySettings.None => "None",
                PrivacySettings.Private => "Private",
                PrivacySettings.FriendsOnly => "Friends Only",
                PrivacySettings.Public => "Public",
                _ => throw new ArgumentOutOfRangeException(nameof(privacySettings), privacySettings, null)
            };
        }

        public static PrivacySettings FromFriendlyString(this string privacySettings)
        {
            return privacySettings.Replace(" ", string.Empty).ToLowerInvariant() switch
            {
                "private" => PrivacySettings.Private,
                "friendsonly" => PrivacySettings.FriendsOnly,
                "public" => PrivacySettings.Public,
                _ => PrivacySettings.None
            };
        }
    }
}
