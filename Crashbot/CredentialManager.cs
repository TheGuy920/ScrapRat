using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crashbot
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/priv.password").Trim();

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/priv.key").Trim().ToCharArray();
    }
}
