using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    // Ban and kick commands are highly similar in implementation, and thus are handled in a single class.
    class BanKick : Command
    {
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
        protected BanKick(ModCommands l, string label, JObject conf, CommandMode mode) : base(l, label, conf)
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

            // Building usage message here
            DefaultUsageMsg = $"{this.Trigger} [user or user ID] " + (_forceReason ? "[reason]" : "*[reason]*") + "\n"
                + "Removes the given user from this server"
                + (_mode == CommandMode.Ban ? " and prevents the user from rejoining" : "") + ". "
                + (_forceReason ? "L" : "Optionally l") + "ogs the reason for the "
                + (_mode == CommandMode.Ban ? "ban" : "kick") + " to the Audit Log.";
            if (_purgeDays > 0)
                DefaultUsageMsg += $"\nAdditionally removes the user's post history for the last {_purgeDays} day(s).";
        }

        #region Strings
        const string FailPrefix = ":x: **Failed to {0} user:** ";
        const string Fail404 = "The specified user is no longer in the server.";
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
                await SendUsageMessageAsync(msg.Channel, null);
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
                    await SendUsageMessageAsync(msg.Channel, ReasonRequired);
                    return;
                }
                reason = null;
            }

            // Retrieve target user
            var (targetId, targetData) = await GetUserDataFromString(g.Id, targetstr);
            if (targetId == 1)
            {
                await msg.Channel.SendMessageAsync(
                    string.Format(FailPrefix, (_mode == CommandMode.Ban ? "ban" : "kick")) + FailDefault);
                return;
            }
            if (targetId == 0)
            {
                await SendUsageMessageAsync(msg.Channel, TargetNotFound);
                return;
            }

            SocketGuildUser targetobj = g.GetUser(targetId);
            string targetdisp;
            if (targetData != null)
                targetdisp = $"{targetData.Username}#{targetData.Discriminator}";
            else
                targetdisp = $"ID {targetId}";

            if (_mode == CommandMode.Kick && targetobj == null)
            {
                // Can't kick without obtaining the user object
                await SendUsageMessageAsync(msg.Channel, TargetNotFound);
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
                await notifyTask;
#warning Remove EscapeDataString call on next Discord.Net update
#if !DEBUG
                if (_mode == CommandMode.Ban) await g.AddBanAsync(targetId, _purgeDays, reasonlog);
                else await targetobj.KickAsync(reasonlog);
#else
#warning "Actual kick/ban action is DISABLED during debug."
#endif
                string resultmsg = BuildSuccessMessage(targetdisp);
                if (notifyTask.Result == false) resultmsg += NotifyFailed;
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
            outresult = outresult.Replace("$s", target.Guild.Name);
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
        public Ban(ModCommands l, string label, JObject conf)
            : base(l, label, conf, CommandMode.Ban) { }
    }

    class Kick : BanKick
    {
        public Kick(ModCommands l, string label, JObject conf)
            : base(l, label, conf, CommandMode.Kick) { }
    }
}
