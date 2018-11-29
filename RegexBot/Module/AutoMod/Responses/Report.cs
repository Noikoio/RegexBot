using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod.Responses
{
    /// <summary>
    /// Sends a summary of the invoking message, along with information
    /// about the rule making use of this command, to the given target.
    /// Parameters: report (target)
    /// </summary>
    class Report : ResponseBase
    {
        readonly string _target;

        public Report(ConfigItem rule, string cmdline) : base(rule, cmdline)
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

            // Discord has a 2000 character limit per single message.
            // Priority is to show as much of the offending line as possible, to a point.
            const int DescriptionLengthMax = 1700; // leaving 300 buffer for embed formatting data
            bool showResponseBody = true;

            if (invokeLine.Length > DescriptionLengthMax)
            {
                // Do not attempt to show response body.
                showResponseBody = false;

                invokeLine = $"**Message length too long; showing first {DescriptionLengthMax} characters.**\n\n"
                    + invokeLine.Substring(0, DescriptionLengthMax);
            }

            string responsebody = null;
            if (showResponseBody)
            {
                // Write a summary of responses defined
                var frb = new StringBuilder();
                foreach (var item in Rule.Response)
                {
                    frb.AppendLine("`" + item.CmdLine.Replace("\r", "").Replace("\n", "\\n") + "`");
                }

                responsebody = frb.ToString();
                if (invokeLine.Length + responsebody.Length > DescriptionLengthMax)
                {
                    // Still can't do it, so just don't.
                    responsebody = null;
                }
            }


            var finalem = new EmbedBuilder()
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
                Name = "Context",
                Value = $"Username: {msg.Author.Mention}\n"
                + $"Channel: <#{msg.Channel.Id}> #{msg.Channel.Name}\n"
                + $"Message ID: {msg.Id}"
            });
            
            if (responsebody != null)
            {
                finalem = finalem.AddField(new EmbedFieldBuilder()
                {
                    Name = "Response:",
                    Value = responsebody.ToString()
                });
            }
            
            return finalem;
        }
    }
}
