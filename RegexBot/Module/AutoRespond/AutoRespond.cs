using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoRespond
{
    /// <summary>
    /// Similar to <see cref="AutoMod"/>, but lightweight.
    /// Provides the capability to define autoresponses for fun or informational purposes.
    /// <para>
    /// The major differences between this and <see cref="AutoMod"/> include:
    /// <list type="bullet">
    /// <item><description>Does not listen for message edits.</description></item>
    /// <item><description>Moderators are not exempt from any defined triggers.</description></item>
    /// <item><description>Responses are limited to the invoking channel.</description></item>
    /// <item><description>Per-channel rate limiting.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    partial class AutoRespond : BotModule
    {
        #region BotModule implementation
        public AutoRespond(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            // Determine channel type - if not a guild channel, stop.
            var ch = arg.Channel as SocketGuildChannel;
            if (ch == null) return;
            
            var defs = GetState<IEnumerable<ConfigItem>>(ch.Guild.Id);
            if (defs == null) return;

            foreach (var def in defs)
                await Task.Run(async () => await ProcessMessage(arg, def));
        }
        
        public override async Task<object> CreateInstanceState(JToken configSection)
        {
            if (configSection == null) return null;
            var responses = new List<ConfigItem>();
            foreach (var def in configSection.Children<JProperty>())
            {
                // All validation is left to the constructor
                var resp = new ConfigItem(def);
                responses.Add(resp);
            }

            if (responses.Count > 0)
                await Log($"Loaded {responses.Count} definition(s) from configuration.");
            return responses.AsReadOnly();
        }
        #endregion

        private async Task ProcessMessage(SocketMessage msg, ConfigItem def)
        {
            if (!def.Match(msg)) return;

            await Log($"'{def.Label}' triggered by {msg.Author} in {((SocketGuildChannel)msg.Channel).Guild.Name}/#{msg.Channel.Name}");

            var (type, text) = def.Response;
            if (type == ConfigItem.ResponseType.Reply) await ProcessReply(msg, text);
            else if (type == ConfigItem.ResponseType.Exec) await ProcessExec(msg, text);
        }

        private async Task ProcessReply(SocketMessage msg, string text)
        {
            await msg.Channel.SendMessageAsync(text);
        }

        private async Task ProcessExec(SocketMessage msg, string text)
        {
            string[] cmdline = text.Split(new char[] { ' ' }, 2);

            ProcessStartInfo ps = new ProcessStartInfo()
            {
                FileName = cmdline[0],
                Arguments = (cmdline.Length == 2 ? cmdline[1] : ""),
                UseShellExecute = false, // ???
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using (Process p = Process.Start(ps))
            {
                p.WaitForExit(5000); // waiting at most 5 seconds
                if (p.HasExited)
                {
                    if (p.ExitCode != 0) await Log("exec: Process returned exit code " + p.ExitCode);
                    using (var stdout = p.StandardOutput)
                    {
                        var result = await stdout.ReadToEndAsync();
                        await msg.Channel.SendMessageAsync(result);
                    }
                }
                else
                {
                    await Log("exec: Process is taking too long to exit. Killing process.");
                    p.Kill();
                    return;
                }
            }
        }
    }
}
