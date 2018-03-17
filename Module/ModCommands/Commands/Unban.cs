using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    class Unban : Command
    {
        // No configuration.
        // TODO bring in some options from BanKick. Particularly custom success msg.
        // TODO when ModLogs fully implemented, add a reason?
        public Unban(CommandListener l, string label, JObject conf) : base(l, label, conf) {
            DefaultUsageMsg = $"{this.Trigger} [user or user ID]\n"
                + "Unbans the given user, allowing them to rejoin the server.";
        }

        #region Strings
        const string FailPrefix = ":x: **Unable to unban:** ";
        protected const string Fail404 = "The specified user does not exist or is not in the ban list.";
        const string TargetNotFound = ":x: **Unable to determine the target user.**";
        const string Success = ":white_check_mark: Unbanned user **{0}**.";
        #endregion

        // Usage: (command) (user query)
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

            // Do the action
            try
            {
                await g.RemoveBanAsync(targetId);
                await msg.Channel.SendMessageAsync(string.Format(Success, targetdisp));
            }
            catch (Discord.Net.HttpException ex)
            {
                if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await msg.Channel.SendMessageAsync(FailPrefix + Fail403);
                }
                else if (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
                {
                    await msg.Channel.SendMessageAsync(FailPrefix + Fail404);
                }
                else
                {
                    await msg.Channel.SendMessageAsync(FailPrefix + FailDefault);
                    await Log(ex.ToString());
                }
            }
        }
    }
}
