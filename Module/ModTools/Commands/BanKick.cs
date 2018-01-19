using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Linq;
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
                _notifyMsg = string.Format(NotifyDefault, mode == CommandMode.Ban ? "banned" : "kicked");
            }
            else
            {
                string val = conf["notifymsg"].Value<string>();
                if (string.IsNullOrWhiteSpace(val)) _notifyMsg = null; // empty value - disable message
                else _notifyMsg = val;
            }
        }

        #region Strings
        const string FailPrefix = ":x: **Failed to {0} user:** ";
        const string Fail403 = "I do not have the required permissions to perform that action.";
        const string Fail404 = "The target user is no longer available.";
        const string FailDefault = "An unknown error occurred. Notify the bot operator.";
        const string NotifyDefault = "You have been {0} from $s for the following reason:\n$r";
        const string NotifyReasonNone = "No reason specified.";
        const string NotifyFailed = "\n(User was unable to receive notification message.)";
        const string ReasonRequired = ":x: **You must specify a reason.**";
        const string TargetNotFound = ":x: **Unable to determine the target user.**";
        #endregion

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
                    await SendUsageMessage(msg, ReasonRequired);
                    return;
                }
                reason = null;
            }

            // Getting SocketGuildUser target
            SocketGuildUser targetobj = null;

            // Extract snowflake value from mention (if a mention was given)
            Match m = UserMention.Match(targetstr);
            if (m.Success) targetstr = m.Groups["snowflake"].Value;

            var qres = (await EntityCache.EntityCache.QueryAsync(g.Id, targetstr)).FirstOrDefault();
            if (qres == null)
            {
                await SendUsageMessage(msg, TargetNotFound);
                return;
            }
            ulong targetuid = qres.UserId;
            targetobj = g.GetUser(targetuid);
            string targetdisp = targetobj?.ToString() ?? $"ID {targetuid}";

            if (_mode == CommandMode.Kick && targetobj == null)
            {
                // Can't kick without obtaining the user object
                await SendUsageMessage(msg, TargetNotFound);
                return;
            }

            // Send out message
            var notifyTask = SendNotificationMessage(targetobj, reason);

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
                if (await notifyTask == false) resultmsg += NotifyFailed;
                await msg.Channel.SendMessageAsync(resultmsg);
            }
            catch (Discord.Net.HttpException ex)
            {
                string err = string.Format(FailPrefix, (_mode == CommandMode.Ban ? "ban" : "kick"));
                if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await msg.Channel.SendMessageAsync(err + Fail403);
                }
                else if (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
                {
                    await msg.Channel.SendMessageAsync(err + Fail404);
                }
                else
                {
                    await msg.Channel.SendMessageAsync(err + FailDefault);
                    await Log(ex.ToString());
                }
            }
        }

        // Returns true on message send success
        private async Task<bool> SendNotificationMessage(SocketGuildUser target, string reason)
        {
            if (_notifyMsg == null) return true;
            if (target == null) return false;

            var ch = await target.GetOrCreateDMChannelAsync();
            string outresult = _notifyMsg;
            outresult = outresult.Replace("$s", g.Name);
            outresult = outresult.Replace("$r", reason ?? NotifyReasonNone);
            try
            {
                await ch.SendMessageAsync(outresult);
            }
            catch (Discord.Net.HttpException ex)
            {
                await Log("Failed to send out notification to target over DM: "
                    + Enum.GetName(typeof(System.Net.HttpStatusCode), ex.HttpCode));
                return false;
            }
            return true;
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
