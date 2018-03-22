using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    class ConfReload : Command
    {
        // No configuration.
        public ConfReload(ModCommands l, string label, JObject conf) : base(l, label, conf) { }

        // Usage: (command)
        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            bool status = await RegexBot.Config.ReloadServerConfig();
            string res;
            if (status) res = ":white_check_mark: Configuration reloaded with no issues. Check the console to verify.";
            else res = ":x: Reload failed. Check the console.";
            await msg.Channel.SendMessageAsync(res);
        }

        // Crazy idea: somehow redirect all logging messages created from invoking config reloading
        // and pass them onto the invoking channel.
    }
}
