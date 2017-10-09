using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools.Commands
{
    
    class BanKick : CommandBase
    {
        // Ban and kick commands are highly similar in implementation, and thus are handled in a single class.
        protected enum CommandMode { Ban, Kick }
        private readonly CommandMode _mode;

        private readonly bool _forceReason;
        private readonly int _purgeDays;
        private readonly string _successMsg;

        // Configuration:
        // "forcereason" - boolean; Force a reason to be given. Defaults to false.
        // "purgedays" - integer; Number of days of target's post history to delete, if banning.
        //               Must be between 0-7 inclusive. Defaults to 0.
        // "successmsg" - Message to display on command success. Overrides default.
        protected BanKick(ModTools l, string label, JObject conf, CommandMode mode) : base(l, label, conf)
        {
            _mode = mode;
            _forceReason = conf["forcereason"]?.Value<bool>() ?? false;
            _purgeDays = conf["purgedays"]?.Value<int>() ?? 0;
            if (_mode == CommandMode.Ban && (_purgeDays > 7 || _purgeDays < 0))
            {
                throw new RuleImportException("The value of 'purgedays' must be between 0 and 7.");
            }
            _successMsg = conf["successmsg"]?.Value<string>();
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
                    await SendUsageMessage(msg, ":x: **You must specify a reason.**");
                    return;
                }
            }

            // Getting SocketGuildUser target
            Match m = UserMention.Match(targetstr);
            if (m.Success) targetstr = m.Groups["snowflake"].Value;

            SocketGuildUser targetobj = null;
            ulong targetuid;
            string targetdisp;
            if (ulong.TryParse(targetstr, out targetuid))
            {
                targetobj = g.GetUser(targetuid);
                targetdisp = (targetobj == null ? $"ID {targetuid}" : targetobj.ToString());
            }
            else
            {
                await SendUsageMessage(msg, ":x: **Unable to determine the target user.**");
                return;
            }

            if (_mode == CommandMode.Kick && targetobj == null)
            {
                // Can't kick without obtaining the user object
                await SendUsageMessage(msg, ":x: **Unable to find the target user.**");
                return;
            }

            try
            {
                if (reason != null) reason = Uri.EscapeDataString(reason); // TODO remove when fixed in library
                if (_mode == CommandMode.Ban) await g.AddBanAsync(targetuid, _purgeDays, reason);
                else await targetobj.KickAsync(reason);
                string resultmsg = BuildSuccessMessage(targetdisp);
                await msg.Channel.SendMessageAsync(resultmsg);
            }
            catch (Discord.Net.HttpException ex)
            {
                string err = ":x: **Failed to " + (_mode == CommandMode.Ban ? "ban" : "kick") + " user:** ";
                if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await msg.Channel.SendMessageAsync(err + "I do not have sufficient permissions to do that action.");
                }
                else if (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
                {
                    await msg.Channel.SendMessageAsync(err + "The target user appears to no longer exist.");
                }
                else
                {
                    await msg.Channel.SendMessageAsync(err + "An unknown error occurred. Details have been logged.");
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

        private string BuildSuccessMessage(string targetstr)
        {
            const string defaultmsgBan = ":white_check_mark: Banned user **$target**.";
            const string defaultmsgKick = ":white_check_mark: Kicked user **$target**.";

            string msg = _successMsg ?? (_mode == CommandMode.Ban ? defaultmsgBan : defaultmsgKick);

            return msg.Replace("$target", targetstr);
        }
    }

    class Ban : BanKick
    {
        public Ban(ModTools l, string label, JObject conf)
            : base(l, label, conf, CommandMode.Ban) { }
    }

    class Kick : BanKick
    {
        public Kick(ModTools l, string label, JObject conf)
            : base(l, label, conf, CommandMode.Kick) { }
    }
}
