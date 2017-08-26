using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoMod.Responses
{
    /// <summary>
    /// Removes the invoking message.
    /// Takes no parameters.
    /// </summary>
    class Remove : Response
    {
        public Remove(Rule rule, string cmdline) : base(rule, cmdline)
        {
            // Throw exception if extra parameters found
            if (cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1)
                throw new RuleImportException("Incorrect number of parameters.");
        }

        public override Task Invoke(SocketMessage msg) => msg.DeleteAsync();
    }
}
