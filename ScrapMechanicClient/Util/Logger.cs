using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ThreadState = System.Threading.ThreadState;

namespace ScrapMechanic
{
    public static class Logger
    {
#if DEBUG
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
#endif
        public enum Verbosity
        {
            None = 0,
            Minimal = 1,
            Normal = 2,
            Verbose = 3,
            Debug = 4
        }

        public static void Log(object? message)
        {
            EnsureRunning();
            LogQueue.Enqueue((LogType.Default, message));
        }

        public static void Log(string format, params object[] args)
        {
            object message = string.Format(format, args);
            Log(message);
        }

        public static void LogInfo(object? message)
        {
            EnsureRunning();
            LogQueue.Enqueue((LogType.Info, message));
        }

        public static void LogInfo(string format, params object[] args)
        {
            object message = string.Format(format, args);
            LogInfo(message);
        }

        public static void LogWarning(object? message)
        {
            EnsureRunning();
            LogQueue.Enqueue((LogType.Warning, message));
        }

        public static void LogWarning(string format, params object[] args)
        {
            object message = string.Format(format, args);
            LogWarning(message);
        }

        public static void LogError(object? message)
        {
            EnsureRunning();
            LogQueue.Enqueue((LogType.Error, message));
        }

        public static void LogError(string format, params object[] args)
        {
            object message = string.Format(format, args);
            LogError(message);
        }

        private static readonly Thread WriteThread = new(Start) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        private static readonly ConcurrentQueue<(LogType, object?)> LogQueue = [];
        private static volatile bool IsRunning = false;

        public static Verbosity LogVerbosity { get; set; } = Verbosity.Debug;

        private static void EnsureRunning()
        {
            if (IsRunning)
                return;

            if (WriteThread.IsAlive)
                throw new InvalidOperationException("Logger is already running. IsRunning is not true. Fatal");

            IsRunning = true;
            WriteThread.Start();
        }

        private static string Timestamp(object? msg) => $"[{DateTime.Now}] {msg}";

        public static uint ThreadId { get; private set; } = 0;

        private static async void Start()
        {
            Directory.CreateDirectory("logs");
            var fstream = File.OpenWrite($"logs/{DateTime.Now:yy-MM-dd-HH-mm-ss}-log.txt");
            Trace.Listeners.Add(new TextWriterTraceListener(fstream));
            Trace.Listeners.Add(new ConsoleTraceListener(false));

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                IsRunning = false;
                WriteThread.Join();
                fstream.Close();
            };

            while (IsRunning)
            {
                while (LogQueue.TryDequeue(out var omsg))
                {
#if DEBUG
                    ThreadId = GetCurrentThreadId();
#endif
                    var (ltype, msg) = omsg;

                    switch (ltype)
                    {
                        case LogType.Info:
                            if (LogVerbosity >= Verbosity.Debug)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Trace.TraceInformation(Timestamp(msg));
                                Console.ResetColor();
                            }
                            break;
                        case LogType.Warning:
                            if (LogVerbosity >= Verbosity.Verbose)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Trace.TraceWarning(Timestamp(msg));
                                Console.ResetColor();
                            }
                            break;
                        case LogType.Error:
                            if (LogVerbosity >= Verbosity.Normal)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Trace.TraceError(Timestamp(msg));
                                Console.ResetColor();
                            }
                            break;
                        default:
                            if (LogVerbosity >= Verbosity.Minimal)
                                Trace.WriteLine(Timestamp(msg));
                            break;
                    }
                }

                await Task.Delay(5);
#if DEBUG
                ThreadId = GetCurrentThreadId();
#endif
            }
        }

        private enum LogType
        {
            Default,
            Info,
            Warning,
            Error
        }
    }
}
