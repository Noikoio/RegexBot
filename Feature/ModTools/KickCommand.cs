using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools
{
    [CommandType("kick")]
    class KickCommand : CommandBase
    {
        private readonly bool _forceReason;

        // Configuration:
        // "forcereason" - boolean; Force a reason to be given. Defaults to false.
        public KickCommand(CommandListener l, JObject conf) : base(l, conf)
        {
            _forceReason = conf["forcereason"]?.Value<bool>() ?? false;
        }
        
        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string targetstr;
            string reason = null;
            if (line.Length < 2)
            {
                await SendUsageMessage(msg, null);
                return;
            }
            targetstr = line[1];

            if (line.Length == 3) reason = line[2]; 
            if (_forceReason && reason == null)
            {
                await SendUsageMessage(msg, ":x: **You must specify a kick reason.**");
                return;
            }

            // Getting SocketGuildUser target
            Match m = UserMention.Match(targetstr);
            if (m.Success) targetstr = m.Groups["snowflake"].Value;

            SocketGuildUser targetobj = null;
            if (ulong.TryParse(targetstr, out var snowflake))
            {
                targetobj = g.GetUser(snowflake);
                if (targetobj == null)
                {
                    await SendUsageMessage(msg, ":x: **Unable to determine the target user.**");
                    return;
                }
            }
            else
            {
                await SendUsageMessage(msg, ":x: **The given target is not valid.**");
                return;
            }
            
            try
            {
                if (reason != null) reason = Uri.EscapeDataString(reason); // TODO remove when fixed in library
                await targetobj.KickAsync(reason);
                await msg.Channel.SendMessageAsync($":white_check_mark: Kicked user **{targetobj.ToString()}**.");
            }
            catch (Discord.Net.HttpException ex)
            {
                const string err = ":x: **Failed to kick user.** ";
                if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await msg.Channel.SendMessageAsync(err + "I do not have permission to do that action.");
                }
                else if (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
                {
                    await msg.Channel.SendMessageAsync(err + "The target user appears to have left the server.");
                }
                else
                {
                    await msg.Channel.SendMessageAsync(err + "An unknown error prevented me from doing that action.");
                    await Log(ex.ToString());
                }
            }
        }

        private async Task SendUsageMessage(SocketMessage m, string message)
        {
            var usageEmbed = new EmbedBuilder()
            {
                Title = "Usage",
                Description = $"{this.Command} [user or user ID] " + (_forceReason ? "[reason]" : "*[reason]*") + "\n" +
                    "Kicks the given user from this server and " + (_forceReason ? "" : "optionally ") +
                    "logs a reason for the kick."
            };
            await m.Channel.SendMessageAsync(message ?? "", embed: usageEmbed);
        }
    }
}
