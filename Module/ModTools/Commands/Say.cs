using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModTools.Commands
{
    class Say : CommandBase
    {
        public Say(ModTools l, string label, JObject conf) : base(l, label, conf) { }

        // TODO: Whitelist/blacklist - to limit which channels it can "say" into

        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length <= 1)
            {
                await SendUsageMessage(msg, ":x: You must specify a channel.");
                return;
            }
            if (line.Length <= 2 || string.IsNullOrWhiteSpace(line[2]))
            {
                await SendUsageMessage(msg, ":x: You must specify a message.");
                return;
            }

            var ch = GetTextChannelFromString(g, line[1]);
            if (ch == null) await SendUsageMessage(msg, ":x: Unable to find given channel.");
            await ch.SendMessageAsync(line[2]);
        }

        private async Task SendUsageMessage(SocketMessage m, string message)
        {
            string desc = $"{this.Command} [channel] [message]\n";
            desc += "Displays the given message exactly as specified to the given channel.";

            var usageEmbed = new EmbedBuilder()
            {
                Title = "Usage",
                Description = desc
            };
            await m.Channel.SendMessageAsync(message ?? "", embed: usageEmbed);
        }

        private SocketTextChannel GetTextChannelFromString(SocketGuild g, string input)
        {
            // Method 1: Check for channel mention
            // Note: SocketGuild.GetTextChannel(ulong) returns null if no match.
            var m = ChannelMention.Match(input);
            if (m.Success)
            {
                ulong channelId = ulong.Parse(m.Groups["snowflake"].Value);
                return g.GetTextChannel(channelId);
            }

            // Method 2: Check if specified in string, scan manually
            if (input.StartsWith('#'))
            {
                input = input.Substring(1);
                if (input.Length <= 0) return null;
                foreach (var c in g.Channels)
                {
                    if (string.Equals(c.Name, input, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return c as SocketTextChannel;
                    }
                }
            }

            return null;
        }
    }
}
