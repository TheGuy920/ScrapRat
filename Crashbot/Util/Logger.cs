using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crashbot.Util
{
    public class Logger
    {
        public static Verbosity LogVerbosity { get; set; } = Verbosity.Normal;

        public static void CheckVerbosity(bool allowed, string msg)
            => Console.Write(allowed ? msg : string.Empty);

        public static void WriteLine(string message, Verbosity level)
            => CheckVerbosity((int)LogVerbosity >= (int)level, $"[{DateTime.Now}] {message}{Environment.NewLine}");

        public static void Write(string message, Verbosity level)
            => CheckVerbosity((int)LogVerbosity >= (int)level, message);

        public static void Clear()
            => Console.Clear();

        public static string? ReadLine()
            => Console.ReadLine();
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
