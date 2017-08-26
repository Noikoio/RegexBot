using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoMod.Responses
{
    /// <summary>
    /// Sends a summary of the invoking message, along with information
    /// about the rule making use of this command, to the given target.
    /// Parameters: report (target)
    /// </summary>
    class Report : Response
    {
        readonly string _target;

        public Report(Rule rule, string cmdline) : base(rule, cmdline)
        {
            var line = cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length != 2) throw new RuleImportException("Incorrect number of parameters");
            _target = line[1];
        }

        public override async Task Invoke(SocketMessage msg)
        {
            var target = await GetMessageTargetAsync(_target, msg);
            if (target == null)
            {
                await Log("Error: Unable to resolve the given target.");
            }
            await target.SendMessageAsync("", embed: BuildReportEmbed(msg));
        }

        private EmbedBuilder BuildReportEmbed(SocketMessage msg)
        {
            string invokeLine = msg.Content;

            var responsebody = new StringBuilder();
            responsebody.AppendLine("```");
            foreach (var item in Rule.Response)
            {
                responsebody.AppendLine(item.CmdLine.Replace("\r", "").Replace("\n", "\\n"));
            }
            responsebody.Append("```");

            // Discord has a 2000 character limit per single message.
            // Enforcing separate length limits on line and response.
            const int DescriptionLengthMax = 1600;
            const int ResponseBodyLengthMax = 200;
            if (invokeLine.Length > DescriptionLengthMax)
            {
                invokeLine = $"**Message length too long; showing first {DescriptionLengthMax} characters.**\n\n"
                    + invokeLine.Substring(0, DescriptionLengthMax);
            }
            if (responsebody.Length > ResponseBodyLengthMax)
            {
                responsebody = new StringBuilder("(Response body too large to display.)");
            }

            return new EmbedBuilder()
            {
                Color = new Color(0xEDCE00), // configurable later?

                Author = new EmbedAuthorBuilder()
                {
                    Name = $"{msg.Author.Username}#{msg.Author.Discriminator} said:",
                    IconUrl = msg.Author.GetAvatarUrl()
                },
                Description = invokeLine,

                Footer = new EmbedFooterBuilder()
                {
                    Text = $"Rule '{Rule.Label}'",
                    IconUrl = Rule.Discord.CurrentUser.GetAvatarUrl()
                },
                Timestamp = msg.EditedTimestamp ?? msg.Timestamp
            }.AddField(new EmbedFieldBuilder()
            {
                Name = "Additional info",
                Value = $"Username: {msg.Author.Mention}\n"
                + $"Channel: <#{msg.Channel.Id}> #{msg.Channel.Name} ({msg.Channel.Id})\n"
                + $"Message ID: {msg.Id}"
            }).AddField(new EmbedFieldBuilder()
            {
                // TODO consider replacing with configurable note. this section is a bit too much
                Name = "Executing response:",
                Value = responsebody.ToString()
            });
        }
    }
}
