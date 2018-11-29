using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod.Responses
{
    /// <summary>
    /// Removes the invoking message.
    /// Takes no parameters.
    /// </summary>
    class Remove : ResponseBase
    {
        public Remove(ConfigItem rule, string cmdline) : base(rule, cmdline)
        {
            // Throw exception if extra parameters found
            if (cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1)
                throw new RuleImportException("Incorrect number of parameters.");
        }

        public override Task Invoke(SocketMessage msg) => msg.DeleteAsync();
    }
}
