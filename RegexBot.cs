using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Main class. On start, initializes bot features and passes the DiscordSocketClient to them
    /// </summary>
    class RegexBot
    {
        private readonly ConfigLoader _config;
        private readonly DiscordSocketClient _client;

        // Constructor loads all subsystems. Subsystem constructors hook up their event delegates.
        internal RegexBot(ConfigLoader conf)
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                MessageCacheSize = 50
            });
            _config = conf;

            // Hook up handlers for basic functions
            _client.Connected += _client_Connected;

            // Initialize features
            new Feature.RegexResponder.EventProcessor(_client, _config);
        }
        
        internal async Task Start()
        {
            var dlog = Logger.GetLogger("Discord");
            _client.Log += async (arg) =>
            await dlog(String.Format("{0}: {1}{2}",
            arg.Source, ((int)arg.Severity < 3 ? arg.Severity + ": " : ""), arg.Message));
            await _client.LoginAsync(TokenType.Bot, _config.BotUserToken);
            await _client.StartAsync();
            
            await Task.Delay(-1);
        }

        private async Task _client_Connected()
        {
            await _client.SetGameAsync(_config.CurrentGame);
            // TODO add support for making use of server invites somewhere around here
        }

        // Defined within this class because a reference to the client is required
        public void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Logger.GetLogger("Runtime")("Caught cancel key. Will attempt to disconnect...").Wait();
            _client.LogoutAsync().Wait();
            _client.Dispose();
#if DEBUG
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
#endif
            Environment.Exit(0);
        }
    }
}
