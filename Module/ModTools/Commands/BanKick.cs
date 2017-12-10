using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModTools.Commands
{
    class BanKick : CommandBase
    {
        // Ban and kick commands are highly similar in implementation, and thus are handled in a single class.
        protected enum CommandMode { Ban, Kick }
        private readonly CommandMode _mode;

        private readonly bool _forceReason;
        private readonly int _purgeDays;
        private readonly string _successMsg;
        private readonly string _notifyMsg;

        const string DefaultMsg = "You have been {0} from $s for the following reason:\n$r";
        const string DefaultMsgBanAppend = "\n\nIf the moderators have allowed it, you may petition your ban by" +
            " submitting **one** message to the moderation team. To do so, reply to this message with" +
            " `!petition [Your message here]`.";

        // Configuration:
        // "forcereason" - boolean; Force a reason to be given. Defaults to false.
        // "purgedays" - integer; Number of days of target's post history to delete, if banning.
        //               Must be between 0-7 inclusive. Defaults to 0.
        // "successmsg" - Message to display on command success. Overrides default.
        // "notifymsg" - Message to send to the target user being acted upon. Default message is used
        //               if the value is not specified. If a blank value is given, the feature is disabled.
        //               Takes the special values $s for server name and $r for reason text.
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
            if (conf["notifymsg"] == null)
            {
                // Message not specified - use default
                _notifyMsg = string.Format(DefaultMsg, mode == CommandMode.Ban ? "banned" : "kicked");
                if (_mode == CommandMode.Ban) _notifyMsg += DefaultMsgBanAppend;
            }
            else
            {
                string val = conf["notifymsg"].Value<string>();
                if (string.IsNullOrWhiteSpace(val)) _notifyMsg = null; // empty value - disable message
                else _notifyMsg = val;
            }
        }

        // Usage: (command) (mention) (reason)
        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string targetstr;
            string reason;
            if (line.Length < 2)
            {
                await SendUsageMessage(msg, null);
                return;
            }
            targetstr = line[1];

            if (line.Length == 3)
            {
                // Reason given - keep it
                reason = line[2];
            }
            else
            {
                // No reason given
                if (_forceReason)
                {
                    await SendUsageMessage(msg, ":x: **You must specify a reason.**");
                    return;
                }
                reason = null;
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

            // Send out message
            bool notifyfail = false;
            if (_notifyMsg != null && targetobj != null)
            {
                var ch = targetobj.GetOrCreateDMChannelAsync();
                string outresult = _notifyMsg;
                outresult = outresult.Replace("$s", g.Name);
                outresult = outresult.Replace("$r", reason ?? "No reason specified.");
                try
                {
                    await (await ch).SendMessageAsync(outresult);
                }
                catch (Discord.Net.HttpException ex)
                {
                    await Log("Failed to send out notification to target over DM: "
                        + Enum.GetName(typeof(System.Net.HttpStatusCode), ex.HttpCode));
                    notifyfail = true;
                }
            }
            else notifyfail = true;

            // Give target user ability to petition
            if (_mode == CommandMode.Ban) Mt.AddPetition(g.Id, targetuid);

            // Do the action
            try
            {
                string reasonlog = $"Invoked by {msg.Author.ToString()}.";
                if (reason != null) reasonlog += $" Reason: {reason}";
                reasonlog = Uri.EscapeDataString(reasonlog);
#warning Remove EscapeDataString call on next Discord.Net update
#if !DEBUG
                if (_mode == CommandMode.Ban) await g.AddBanAsync(targetuid, _purgeDays, reasonlog);
                else await targetobj.KickAsync(reason);
#else
#warning "Actual kick/ban action is DISABLED during debug."
#endif
                string resultmsg = BuildSuccessMessage(targetdisp);
                if (notifyfail)
                {
                    resultmsg += $"\n(could not send " + (_mode == CommandMode.Ban ? "ban" : "kick") + " notification to user.)";
                }
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
