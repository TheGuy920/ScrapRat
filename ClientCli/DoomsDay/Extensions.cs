using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ScrapRatCli.DoomsDay
{
    internal static class Extensions
    {
        public static T FromJsonTo<T>(this string json)
        {
            string fjson = json.RemoveCommentsFromJson();
            return JsonConvert.DeserializeObject<T>(fjson)!;
        }

        private static string RemoveCommentsFromJson(this string jsonString)
        {
            // Pattern to remove single-line and multi-line comments
            string pattern = @"(\/\/.*$|\/\*[\s\S]*?\*\/)";
            Regex regex = new(pattern, RegexOptions.Multiline);
            return regex.Replace(jsonString, string.Empty);
        }
    }
}
