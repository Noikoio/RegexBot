using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    // Role adding and removing is largely the same, and thus are handled in a single class.
    class RoleManipulation : Command
    {
        protected enum CommandMode { Add, Del }
        private readonly CommandMode _mode;

        private readonly EntityName _role;
        private readonly string _successMsg;
        // Configuration:
        // "role" - string; The given role that applies to this command.
        // "successmsg" - string; Messages to display on command success. Overrides default.

        protected RoleManipulation(ModCommands l, string label, JObject conf, CommandMode mode) : base(l, label, conf)
        {
            _mode = mode;
            var rolestr = conf["role"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(rolestr)) throw new RuleImportException("Role must be provided.");
            _role = new EntityName(rolestr, EntityType.Role);
            _successMsg = conf["successmsg"]?.Value<string>();

            DefaultUsageMsg = $"{this.Trigger} [user or user ID]\n"
                + (_mode == CommandMode.Add ? "Adds" : "Removes") + " the specified role "
                + (_mode == CommandMode.Add ? "to" : "from") + " the given user.";
        }

        #region Strings
        const string FailPrefix = ":x: **Failed to apply role change:** ";
        const string TargetNotFound = ":x: **Unable to determine the target user.**";
        const string RoleNotFound = ":x: **Failed to determine the specified role for this command.**";
        const string Success = ":white_check_mark: Successfully {0} role for **{1}**.";
        #endregion

        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string targetstr;
            if (line.Length < 2)
            {
                await SendUsageMessageAsync(msg.Channel, null);
                return;
            }
            targetstr = line[1];

            // Retrieve target user
            var (targetId, targetData) = await GetUserDataFromString(g.Id, targetstr);
            if (targetId == 1)
            {
                await msg.Channel.SendMessageAsync(FailPrefix + FailDefault);
                return;
            }
            if (targetId == 0)
            {
                await SendUsageMessageAsync(msg.Channel, TargetNotFound);
                return;
            }

            string targetdisp;
            if (targetData != null)
                targetdisp = $"{targetData.Username}#{targetData.Discriminator}";
            else
                targetdisp = $"ID {targetId}";

            // Determine role
            SocketRole cmdRole;
            if (_role.Id.HasValue)
            {
                cmdRole = g.GetRole(_role.Id.Value);
            }
            else
            {
                var res = g.Roles.Where(rn => 
                    string.Equals(rn.Name, _role.Name, StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();
                if (res == null)
                {
                    await msg.Channel.SendMessageAsync(RoleNotFound);
                    await Log(RoleNotFound);
                    return;
                }
                cmdRole = res;
            }

            // Do the action
            try
            {
                var u = g.GetUser(targetId);
                if (_mode == CommandMode.Add)
                    await u.AddRoleAsync(cmdRole);
                else
                    await u.RemoveRoleAsync(cmdRole);
                await msg.Channel.SendMessageAsync(BuildSuccessMessage(targetdisp));
            }
            catch (Discord.Net.HttpException ex)
            {
                if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await msg.Channel.SendMessageAsync(FailPrefix + Fail403);
                }
                else
                {
                    await msg.Channel.SendMessageAsync(FailPrefix + FailDefault);
                    await Log(ex.ToString());
                }
            }
        }

        private string BuildSuccessMessage(string targetstr)
        {
            const string defaultmsg = ":white_check_mark: Successfully {0} role for **$target**.";
            string msg = _successMsg ?? string.Format(defaultmsg, _mode == CommandMode.Add ? "set" : "unset");
            return msg.Replace("$target", targetstr);
        }
    }

    class RoleAdd : RoleManipulation
    {
        public RoleAdd(ModCommands l, string label, JObject conf) : base(l, label, conf, CommandMode.Add) { }
    }

    class RoleDel : RoleManipulation
    {
        public RoleDel(ModCommands l, string label, JObject conf) : base(l, label, conf, CommandMode.Del) { }
    }
}
