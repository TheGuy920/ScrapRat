using System.Collections.Concurrent;
using System.Net;
using PacketMonitor;
using System.Diagnostics;


namespace IpProxyServerLogging
{
    public class Program
    {
        private const int WebApiPort = 5108;

        public static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            MonitorServer proxyserver = new();
            proxyserver.Start();
            Console.WriteLine("Server started.");
            /*
            var builder = WebApplication.CreateSlimBuilder(args);
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.WriteIndented = true;
                options.SerializerOptions.AllowTrailingCommas = true;
            });

            var app = builder.Build();
            app.Urls.Add($"http://localhost:{WebApiPort}");

            app.MapGet("/ip", async (HttpContext context) =>
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;

                string json = JsonConvert.SerializeObject(new byte[0], Formatting.Indented);
                using var writer = new StreamWriter(context.Response.Body);
                Console.WriteLine(json);
                await writer.WriteAsync(json);
                await writer.DisposeAsync();
            });
            */
            //app.Run();

            while (true)
            {
                Console.ReadKey();
            }
        }
    }
}
