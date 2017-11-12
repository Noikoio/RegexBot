using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod.Responses
{
    /// <summary>
    /// Kicks the invoking user.
    /// Takes no parameters.
    /// </summary>
    class Kick : ResponseBase
    {
        public Kick(ConfigItem rule, string cmdline) : base(rule, cmdline)
        {
            // Throw exception if extra parameters found
            if (cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1)
                throw new RuleImportException("Incorrect number of parameters.");
        }

        public override async Task Invoke(SocketMessage msg)
        {
            var target = (SocketGuildUser)msg.Author;
            await target.KickAsync(Uri.EscapeDataString($"Rule '{Rule.Label}'"));
#warning Remove EscapeDataString call on next Discord.Net update
        }
    }
}
