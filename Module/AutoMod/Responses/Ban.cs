using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod.Responses
{
    /// <summary>
    /// Bans the invoking user.
    /// Parameters: ban [days = 0]
    /// </summary>
    class Ban : ResponseBase
    {
        readonly int _purgeDays;

        public Ban(ConfigItem rule, string cmdline) : base(rule, cmdline)
        {
            var line = cmdline.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length == 1)
            {
                _purgeDays = 0;
            }
            else if (line.Length == 2)
            {
                if (int.TryParse(line[1], out _purgeDays))
                {
                    if (_purgeDays < 0 || _purgeDays > 7)
                    {
                        throw new RuleImportException("Parameter must be an integer between 0 and 7.");
                    }
                }
                else
                {
                    throw new RuleImportException("Parameter must be an integer between 0 and 7.");
                }
            }
            else
            {
                throw new RuleImportException("Incorrect number of parameters.");
            }
        }

        public override async Task Invoke(SocketMessage msg)
        {
            var target = (SocketGuildUser)msg.Author;
            await target.Guild.AddBanAsync(target, _purgeDays, Uri.EscapeDataString($"Rule '{Rule.Label}'"));
#warning Remove EscapeDataString call on next Discord.Net update
        }
    }
}
