using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            await CommandCheckInvoke(arg);
        }

        [ConfigSection("modtools")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            // TODO: put command definitions elsewhere, not in root of this config

            var commands = new Dictionary<string, CommandBase>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in configSection.Children<JProperty>())
            {
                string label = def.Name;
                var cmd = CommandBase.CreateInstance(this, def);
                if (commands.ContainsKey(cmd.Command))
                    throw new RuleImportException(
                        $"{label}: 'command' value must not be equal to that of another definition. " +
                        $"Given value is being used for {commands[cmd.Command].Label}.");

                commands.Add(cmd.Command, cmd);
            }
            await Log($"Loaded {commands.Count} command definition(s).");
            return new ReadOnlyDictionary<string, CommandBase>(commands);
        }

        public new Task Log(string text) => base.Log(text);

        private async Task CommandCheckInvoke(SocketMessage arg)
        {
            // Disregard if not in a guild
            SocketGuild g = (arg.Author as SocketGuildUser)?.Guild;
            if (g == null) return;

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
            if (((IDictionary<string, CommandBase>)GetConfig(g.Id)).TryGetValue(cmdchk, out var c))
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
