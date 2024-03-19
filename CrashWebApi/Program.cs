namespace CrashWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            File.WriteAllText("steam_appid.txt", "387990");

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
