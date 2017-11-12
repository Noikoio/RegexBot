using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod.Responses
{
    /// <summary>
    /// Manipulates a given user's role.
    /// Parameters: (command) (target) (role ID)
    /// </summary>
    class RoleManipulation : ResponseBase
    {
        enum ManipulationType { None, Add, Remove }

        readonly ManipulationType _action;
        readonly string _target;
        readonly EntityName _role;

        public RoleManipulation(ConfigItem rule, string cmdline) : base(rule, cmdline)
        {
            var line = cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length != 3)
                throw new RuleImportException("Incorrect number of parameters.");

            // Ensure the strings here match those in Response._commands
            switch (line[0].ToLowerInvariant())
            {
                case "addrole":
                case "grantrole":
                    _action = ManipulationType.Add;
                    break;
                case "delrole":
                case "removerole":
                case "revokerole":
                    _action = ManipulationType.Remove;
                    break;
                default:
                    _action = ManipulationType.None;
                    break;
            }
            if (_action == ManipulationType.None)
                throw new RuleImportException("Command not defined. This is a bug.");

            _target = line[1];
            _role = new EntityName(line[2], EntityType.Role);
        }

        public override async Task Invoke(SocketMessage msg)
        {
            // Find role
            SocketRole rtarget;
            var g = ((SocketGuildUser)msg.Author).Guild;
            if (_role.Id.HasValue) rtarget = g.GetRole(_role.Id.Value);
            else rtarget = g.Roles.FirstOrDefault(r =>
                string.Equals(r.Name, _role.Name, StringComparison.OrdinalIgnoreCase));
            if (rtarget == null)
            {
                await Log("Error: Target role not found in server.");
                return;
            }

            // Find user
            SocketGuildUser utarget;
            if (_target == "@_") utarget = (SocketGuildUser)msg.Author;
            else
            {
                utarget = g.Users.FirstOrDefault(u =>
                {
                    if (string.Equals(u.Nickname, _target, StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(u.Username, _target, StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                });
            }
            if (utarget == null)
            {
                await Log("Error: Target user not found in server.");
                return;
            }

            // Do action
            if (_action == ManipulationType.Add)
                await utarget.AddRoleAsync(rtarget);
            else if (_action == ManipulationType.Remove)
                await utarget.RemoveRoleAsync(rtarget);
        }
    }
}
