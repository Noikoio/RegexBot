using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;
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
        const string Fail403 = "I do not have the required permissions to perform that action.";
        const string Fail404 = "The target user is no longer available.";
        const string FailDefault = "An unknown error occurred. Notify the bot operator.";
        const string TargetNotFound = ":x: **Unable to determine the target user.**";
        const string Success = ":white_check_mark: Unbanned user **{0}**.";
        #endregion

        // Usage: (command) (user query)
        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            // TODO oh god there's so much boilerplate copypasted from BanKick make it stop

            string[] line = msg.Content.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string targetstr;
            if (line.Length < 2)
            {
                await SendUsageMessageAsync(msg.Channel, null);
                return;
            }
            targetstr = line[1];

            // Getting SocketGuildUser target
            SocketGuildUser targetobj = null;

            // Extract snowflake value from mention (if a mention was given)
            Match m = UserMention.Match(targetstr);
            if (m.Success) targetstr = m.Groups["snowflake"].Value;

            EntityCache.CacheUser qres;
            try
            {
                qres = (await EntityCache.EntityCache.QueryAsync(g.Id, targetstr)).FirstOrDefault();
            }
            catch (Npgsql.NpgsqlException ex)
            {
                await Log("A database error occurred during user lookup: " + ex.Message);
                await msg.Channel.SendMessageAsync(FailPrefix + FailDefault);
                return;
            }
            
            if (qres == null)
            {
                await SendUsageMessageAsync(msg.Channel, TargetNotFound);
                return;
            }

            ulong targetuid = qres.UserId;
            targetobj = g.GetUser(targetuid);
            string targetdisp = targetobj?.ToString() ?? $"ID {targetuid}";

            // Do the action
            try
            {
                await g.RemoveBanAsync(targetuid);
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
