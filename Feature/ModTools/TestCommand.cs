using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools
{
#if DEBUG
    [CommandType("test")]
    class TestCommand : CommandBase
    {
        public TestCommand(CommandListener l, JObject conf) : base(l, conf) {
            bool? doCrash = conf["crash"]?.Value<bool>();
            if (doCrash.HasValue && doCrash.Value)
                throw new RuleImportException("Throwing exception in constructor upon request.");
        }

        public override async Task Invoke(SocketGuild g, SocketMessage msg)
        {
            await msg.Channel.SendMessageAsync("This is the test command. It is labeled: " + this.Label);
        }
    }
#endif
}
