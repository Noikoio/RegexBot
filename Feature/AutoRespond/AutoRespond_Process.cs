using Discord.WebSocket;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoRespond
{
    partial class AutoRespond
    {
        private async Task ProcessMessage(SocketMessage msg, ResponseDefinition def)
        {
            // Check filters
            if (def.Filter.IsFiltered(msg)) return;

            // Check rate limit
            if (!def.RateLimit.AddUsage(msg.Channel.Id)) return;
            
            // Check if the trigger is a match, of course
            if (!def.Trigger.IsMatch(msg.Content)) return;

            await Log($"'{def.Label}' triggered in #{msg.Channel.Name} by {msg.Author}");
            var (type, text) = def.Response;
            if (type == ResponseDefinition.ResponseType.Reply) await ProcessReply(msg, text);
            else if (type == ResponseDefinition.ResponseType.Exec) await ProcessExec(msg, text);
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
