using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModTools
{
    /// <summary>
    /// ModTools module.
    /// This class manages reading configuration and creating instances based on it.
    /// </summary>
    class ModTools : BotModule
    {
        public override string Name => "ModTools";
        
        public ModTools(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            // Always ignore bots
            if (arg.Author.IsBot) return;

            if (arg.Channel is IGuildChannel) await CommandCheckInvoke(arg);
        }
        
        [ConfigSection("ModTools")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            // Constructor throws exception on config errors
            var conf = new ConfigItem(this, configSection);

            // Log results
            if (conf.Commands.Count > 0)
                await Log(conf.Commands.Count + " command definition(s) loaded.");

            return conf;
        }

        private new ConfigItem GetConfig(ulong guildId) => (ConfigItem)base.GetConfig(guildId);

        public new Task Log(string text) => base.Log(text);

        private async Task CommandCheckInvoke(SocketMessage arg)
        {
            SocketGuild g = ((SocketGuildUser)arg.Author).Guild;

            // Get guild config
            ServerConfig sc = RegexBot.Config.Servers.FirstOrDefault(s => s.Id == g.Id);
            if (sc == null) return;

            // Disregard if not a bot moderator
            if (!sc.Moderators.ExistsInList(arg)) return;

            // Disregard if the message contains a newline character
            if (arg.Content.Contains("\n")) return;

            // Check for and invoke command
            string cmdchk;
            int spc = arg.Content.IndexOf(' ');
            if (spc != -1) cmdchk = arg.Content.Substring(0, spc);
            else cmdchk = arg.Content;
            if (GetConfig(g.Id).Commands.TryGetValue(cmdchk, out var c))
            {
                try
                {
                    await Log($"'{c.Label}' invoked by {arg.Author.ToString()} in {g.Name}/#{arg.Channel.Name}");
                    await c.Invoke(g, arg);
                }
                catch (Exception ex)
                {
                    await Log($"Encountered an error for the command '{c.Label}'. Details follow:");
                    await Log(ex.ToString());
                }
            }
        }
    }
}
