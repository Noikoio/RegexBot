using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools
{
    [CommandType("ban")]
    class BanCommand : CommandBase
    {
        private readonly bool _forceReason;
        private readonly int _purgeDays;

        // Configuration:
        // "forcereason" - boolean; Force a reason to be given. Defaults to false.
        // "purgedays" - integer; Number of days of target's post history to delete. Must be between 0-7 inclusive.
        //               Defaults to 0.
        public BanCommand(CommandListener l, JObject conf) : base(l, conf)
        {
            _forceReason = conf["forcereason"]?.Value<bool>() ?? false;
            _purgeDays = conf["purgedays"]?.Value<int>() ?? 0;
            if (_purgeDays > 7 || _purgeDays < 0)
            {
                throw new RuleImportException("The value of 'purgedays' must be between 0 and 7.");
            }
        }

        // Usage: (command) (mention) (reason)
        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string targetstr;
            string reason = $"Invoked by {msg.Author.ToString()}.";
            if (line.Length < 2)
            {
                await SendUsageMessage(msg, null);
                return;
            }
            targetstr = line[1];

            if (line.Length == 3)
            {
                // Reason exists
                reason += " Reason: " + line[2];
            }
            else
            {
                // No reason given
                if (_forceReason)
                {
                    await SendUsageMessage(msg, ":x: **You must specify a ban reason.**");
                    return;
                }
            }

            // Getting SocketGuildUser kick target (ensuring that it's the parameter)
            SocketGuildUser targetobj = null;
            if (UserMention.IsMatch(targetstr))
            {
                targetobj = msg.MentionedUsers.ElementAt(0) as SocketGuildUser;
            }
            else if (ulong.TryParse(targetstr, out var snowflake))
            {
                targetobj = g.GetUser(snowflake);
            }

            if (targetobj == null)
            {
                await SendUsageMessage(msg, ":x: **Unable to determine the target user.**");
                return;
            }

            try
            {
                await g.AddBanAsync(targetobj, _purgeDays, reason);
                await msg.Channel.SendMessageAsync($":white_check_mark: Banned user **{targetobj.ToString()}**.");
            }
            catch (Discord.Net.HttpException ex)
            {
                const string err = ":x: **Failed to ban user.** ";
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
            string desc = $"{this.Command} [user or user ID] " + (_forceReason ? "[reason]" : "*[reason]*") + "\n";
            desc += "Removes the given user from this server and prevents the user from rejoining. ";
            desc += (_forceReason ? "L" : "Optionally l") + "ogs the reason for the ban to the Audit Log.";
            if (_purgeDays > 0)
                desc += $"\nAdditionally removes the user's post history for the last {_purgeDays} day(s).";

            var usageEmbed = new EmbedBuilder()
            {
                Title = "Usage",
                Description = desc
            };
            await m.Channel.SendMessageAsync(message ?? "", embed: usageEmbed);
        }
    }
}
