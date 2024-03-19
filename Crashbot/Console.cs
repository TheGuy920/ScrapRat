using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crashbot
{
    public class Console
    {
        public static void WriteLine(string message)
            => System.Console.WriteLine($"[{DateTime.Now}] {message}");

        public static void Write(string message)
            => System.Console.Write(message);

        public static void Clear()
            => System.Console.Clear();

        public static string? ReadLine()
            => System.Console.ReadLine();
    }
}
