using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    class Say : Command
    {
        // No configuration at the moment.
        // TODO: Whitelist/blacklist - to limit which channels it can "say" into
        public Say(CommandListener l, string label, JObject conf) : base(l, label, conf) {
            DefaultUsageMsg = $"{this.Trigger} [channel] [message]\n"
                + "Displays the given message exactly as specified to the given channel.";
        }

        #region Strings
        const string ChannelRequired = ":x: You must specify a channel.";
        const string MessageRequired = ":x: You must specify a message.";
        const string TargetNotFound = ":x: Unable to find given channel.";
        #endregion

        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length <= 1)
            {
                await SendUsageMessageAsync(msg.Channel, ChannelRequired);
                return;
            }
            if (line.Length <= 2 || string.IsNullOrWhiteSpace(line[2]))
            {
                await SendUsageMessageAsync(msg.Channel, MessageRequired);
                return;
            }

            var ch = GetTextChannelFromString(g, line[1]);
            if (ch == null) await SendUsageMessageAsync(msg.Channel, TargetNotFound);
            await ch.SendMessageAsync(line[2]);
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
