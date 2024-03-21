using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Reflection;

namespace CrashbotWebApi.Discord
{
    public class DiscordBot
    {
        private DiscordSocketClient Client { get; }
        private CommandService Commands { get; }
        private IServiceProvider Services { get; }

        public DiscordBot()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });

            Commands = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                LogLevel = LogSeverity.Debug
            });

            Services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton(Commands)
                .BuildServiceProvider();
        }

        public async Task InitializeAsync()
        {
            await RegisterCommandsAsync();
            await Client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
            await Client.StartAsync();
        }

        private async Task RegisterCommandsAsync()
        {
            Client.MessageReceived += HandleCommandAsync;
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), Services);
        }

        private async Task HandleCommandAsync(SocketMessage message)
        {
            if (!(message is SocketUserMessage userMessage) || message.Author.IsBot)
            {
                return;
            }

            int argPos = 0;
            if (userMessage.HasStringPrefix("!", ref argPos) || userMessage.HasMentionPrefix(Client.CurrentUser, ref argPos))
            {
                SocketCommandContext context = new SocketCommandContext(Client, userMessage);
                await Commands.ExecuteAsync(context, argPos, Services);
            }
        }
    }
}
