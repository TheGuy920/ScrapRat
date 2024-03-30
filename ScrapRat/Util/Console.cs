using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapRat.Util
{
    public class ConsoleVerbosity
    {
        public enum Verbosity
        {
            None = 0x00,
            Info = 0x01,
            Warn = 0x02,
            Error = 0x03,
            Debug = 0x04,
        }

        public static void SetVerbosity(Verbosity level)
        {
            Console.Level = level;
        }
    }

    internal class Console
    {
        internal static ConsoleVerbosity.Verbosity Level { get; set; } = ConsoleVerbosity.Verbosity.None;

        public static void WriteLine(object? message)
        {
            System.Console.WriteLine(message);
        }

        public static void WriteLine(string format, params object[] args)
        {
            System.Console.WriteLine(format, args);
        }

        public static void Write(object? message)
        {
            System.Console.Write(message);
        }

        public static void Write(string format, params object[] args)
        {
            System.Console.Write(format, args);
        }

        public static void WarnLine(object? message)
        {
            if (Level < ConsoleVerbosity.Verbosity.Warn)
                return;

            System.Console.ForegroundColor = ConsoleColor.DarkYellow;
            System.Console.WriteLine(message);
            System.Console.ResetColor();
        }

        public static void WarnLine(string format, params object[] args)
        {
            if (Level < ConsoleVerbosity.Verbosity.Warn)
                return;

            System.Console.ForegroundColor = ConsoleColor.DarkYellow;
            System.Console.WriteLine(format, args);
            System.Console.ResetColor();
        }

        public static void ErrorLine(object? message)
        {
            if (Level < ConsoleVerbosity.Verbosity.Error)
                return;

            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(message);
            System.Console.ResetColor();
        }

        public static void ErrorLine(string format, params object[] args)
        {
            if (Level < ConsoleVerbosity.Verbosity.Error)
                return;

            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(format, args);
            System.Console.ResetColor();
        }

        public static void InfoLine(object? message)
        {
            if (Level < ConsoleVerbosity.Verbosity.Info)
                return;

            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine(message);
            System.Console.ResetColor();
        }

        public static void InfoLine(string format, params object[] args)
        {
            if (Level < ConsoleVerbosity.Verbosity.Info)
                return;

            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine(format, args);
            System.Console.ResetColor();
        }
    }
}
