using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Main class. On start, initializes bot features and passes the DiscordSocketClient to them
    /// </summary>
    class RegexBot
    {
        private static Configuration _config;
        private readonly DiscordSocketClient _client;
        private BotFeature[] _features;

        internal static Configuration Config => _config;
        internal IEnumerable<BotFeature> Features => _features;
        
        internal RegexBot()
        {
            // Load configuration
            _config = new Configuration(this);
            if (!_config.LoadInitialConfig())
            {
#if DEBUG
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
#endif
                Environment.Exit(1);
            }

            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                MessageCacheSize = 50
            });

            // Hook up handlers for basic functions
            _client.Connected += _client_Connected;

            // Initialize features
            _features = new BotFeature[]
            {
                new Feature.RegexResponder.EventProcessor(_client),
                new Feature.ModTools.CommandListener(_client)
            };
            var dlog = Logger.GetLogger("Discord.Net");
            _client.Log += async (arg) =>
                await dlog(
                    String.Format("{0}: {1}{2}", arg.Source, ((int)arg.Severity < 3 ? arg.Severity + ": " : ""),
                    arg.Message));

            // With features initialized, finish loading configuration
            if (!_config.ReloadServerConfig().GetAwaiter().GetResult())
            {
                Console.WriteLine("Failed to load server configuration.");
#if DEBUG
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
#endif
                Environment.Exit(1);
            }
        }
        
        internal async Task Start()
        {
            
            await _client.LoginAsync(TokenType.Bot, Config.BotUserToken);
            await _client.StartAsync();
            
            await Task.Delay(-1);
        }

        private async Task _client_Connected()
        {
            await _client.SetGameAsync(Config.CurrentGame);
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
