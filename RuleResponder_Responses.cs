using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    // Contains code for handling each response in a rule.
    partial class RuleResponder
    {
        private delegate Task ResponseProcessor(AsyncLogger l, string cmd, Rule r, SocketMessage m);
        private readonly ReadOnlyDictionary<string, ResponseProcessor> _commands;

#if DEBUG
        /// <summary>
        /// Throws an exception. Meant to be a quick error handling test.
        /// No parameters.
        /// </summary>
        private async Task RP_Crash(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            await l("Will throw an exception.");
            throw new Exception("Requested in response.");
        }

        /// <summary>
        /// Prints all guild values (IDs for users, channels, roles) to console.
        /// The guild info displayed is the one in which the command is invoked.
        /// No parameters.
        /// </summary>
        private Task RP_DumpID(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            var g = ((SocketGuildUser)m.Author).Guild;
            var result = new StringBuilder();
            
            result.AppendLine("Users:");
            foreach (var item in g.Users)
                result.AppendLine($"{item.Id} {item.Username}#{item.Discriminator}");
            result.AppendLine();

            result.AppendLine("Channels:");
            foreach (var item in g.Channels) result.AppendLine($"{item.Id} #{item.Name}");
            result.AppendLine();
            result.AppendLine("Roles:");
            foreach (var item in g.Roles) result.AppendLine($"{item.Id} {item.Name}");
            result.AppendLine();

            Console.WriteLine(result.ToString());
            return Task.CompletedTask;
        }
#endif
        /// <summary>
        /// Sends a message to a specified channel.
        /// Parameters: say (channel) (message)
        /// </summary>
        private async Task RP_Say(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            string[] @in = SplitParams(cmd, 3);
            if (@in.Length != 3)
            {
                await l("Error: say: Incorrect number of parameters.");
                return;
            }

            var target = await GetMessageTargetAsync(@in[1], m);
            if (target == null)
            {
                await l("Error: say: Unable to resolve given target.");
                return;
            }

            // ﻿ＣＨＡＮＧＥ  ＴＨＥ  ＳＡＹ
            @in[2] = ProcessText(@in[2], m);
            await target.SendMessageAsync(@in[2]);
        }

        /// <summary>
        /// Reports the incoming message to a given channel.
        /// Parameters: report (channel)
        /// </summary>
        private async Task RP_Report(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            string[] @in = SplitParams(cmd);
            if (@in.Length != 2)
            {
                await l("Error: report: Incorrect number of parameters.");
                return;
            }

            var target = await GetMessageTargetAsync(@in[1], m);
            if (target == null)
            {
                await l("Error: report: Unable to resolve given target.");
                return;
            }


            var responsefield = new StringBuilder();
            responsefield.AppendLine("```");
            foreach (var line in r.Responses)
                responsefield.AppendLine(line.Replace("\r", "").Replace("\n", "\\n"));
            responsefield.Append("```");
            await target.SendMessageAsync("", embed: new EmbedBuilder()
            {
                Color = new Color(0xEDCE00), // configurable later?

                Author = new EmbedAuthorBuilder()
                {
                    Name = $"{m.Author.Username}#{m.Author.Discriminator} said:",
                    IconUrl = m.Author.GetAvatarUrl()
                },
                Description = m.Content,

                Footer = new EmbedFooterBuilder()
                {
                    Text = $"Rule '{r.DisplayName}'",
                    IconUrl = _client.CurrentUser.GetAvatarUrl()
                },
                Timestamp = m.Timestamp
            }.AddField(new EmbedFieldBuilder()
            {
                Name = "Additional info",
                Value = $"Channel: <#{m.Channel.Id}>\n" // NOTE: manually mentioning channel here
                + $"Username: {m.Author.Mention}\n"
                + $"Message ID: {m.Id}"
            }).AddField(new EmbedFieldBuilder()
            {
                Name = "Executing response:",
                Value = responsefield.ToString()
            }));
        }

        /// <summary>
        /// Deletes the incoming message.
        /// No parameters.
        /// </summary>
        private async Task RP_Remove(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            // Parameters are not checked
            await m.DeleteAsync();
        }

        /// <summary>
        /// Executes an external program and sends standard output to the given channel.
        /// Parameters: exec (channel) (command line)
        /// </summary>
        private async Task RP_Exec(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            var @in = SplitParams(cmd, 4);
            if (@in.Length < 3)
            {
                await l("exec: Incorrect number of parameters.");
            }

            string result;
            var target = await GetMessageTargetAsync(@in[1], m);
            if (target == null)
            {
                await l("Error: exec: Unable to resolve given channel.");
                return;
            }

            ProcessStartInfo ps = new ProcessStartInfo()
            {
                FileName = @in[2],
                Arguments = (@in.Length > 3 ? @in[3] : ""),
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using (Process p = Process.Start(ps))
            {
                p.WaitForExit(5000); // waiting at most 5 seconds
                if (p.HasExited)
                {
                    if (p.ExitCode != 0) await l("exec: Process returned exit code " + p.ExitCode);
                    using (var stdout = p.StandardOutput)
                    {
                        result = await stdout.ReadToEndAsync();
                    }
                }
                else
                {
                    await l("exec: Process is taking too long to exit. Killing process.");
                    p.Kill();
                    return;
                }
            }
            
            result = ProcessText(result.Trim(), m);
            await target.SendMessageAsync(result);
        }

        /// <summary>
        /// Bans the sender of the incoming message.
        /// No parameters.
        /// </summary>
        // TODO add parameter for message auto-deleting
        private async Task RP_Ban(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            SocketGuild g = ((SocketGuildUser)m.Author).Guild;
            await g.AddBanAsync(m.Author);
        }

        /// <summary>
        /// Grants or revokes a specified role to/from a given user.
        /// Parameters: grantrole/revokerole (user ID or @_) (role ID)
        /// </summary>
        private async Task RP_GrantRevokeRole(AsyncLogger l, string cmd, Rule r, SocketMessage m)
        {
            string[] @in = SplitParams(cmd);
            if (@in.Length != 3)
            {
                await l($"Error: {@in[0]}: incorrect number of parameters.");
                return;
            }
            if (!ulong.TryParse(@in[2], out var roleID))
            {
                await l($"Error: {@in[0]}: Invalid role ID specified.");
                return;
            }

            // Finding role
            var gu = (SocketGuildUser)m.Author;
            SocketRole rl = gu.Guild.GetRole(roleID);
            if (rl == null)
            {
                await l($"Error: {@in[0]}: Specified role not found.");
                return;
            }

            // Finding user
            SocketGuildUser target;
            if (@in[1] == "@_")
            {
                target = gu;
            } 
            else
            {
                if (!ulong.TryParse(@in[1], out var userID))
                {
                    await l($"Error: {@in[0]}: Invalid user ID specified.");
                    return;
                }
                target = gu.Guild.GetUser(userID);
                if (target == null)
                {
                    await l($"Error: {@in[0]}: Given user ID does not exist in this server.");
                    return;
                }
            }

            if (@in[0].ToLower() == "grantrole")
            {
                await target.AddRoleAsync(rl);
            }
            else
            {
                await target.RemoveRoleAsync(rl);
            }
        }

        
    }
}
