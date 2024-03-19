using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crashbot
{
    public class Console
    {
        public static Verbosity LogVerbosity { get; set; } = Verbosity.Normal;

        public static void CheckVerbosity(bool allowed, string msg)
            => System.Console.Write(allowed ? msg : string.Empty);

        public static void WriteLine(string message, Verbosity level)
            => Console.CheckVerbosity(Console.LogVerbosity >= level, $"[{DateTime.Now}] {message}{Environment.NewLine}");

        public static void Write(string message, Verbosity level)
            => Console.CheckVerbosity(Console.LogVerbosity >= level, message);

        public static void Clear()
            => System.Console.Clear();

        public static string? ReadLine()
            => System.Console.ReadLine();
    }

    public enum Verbosity
    {
        None = 0,
        Minimal = 1,
        Normal = 2,
        Verbose = 3,
        Debug = 4
    }
}
