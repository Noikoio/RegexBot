using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoMod.Responses
{
    /// <summary>
    /// Sends a message to the given target.
    /// Parameters: say (target) (message)
    /// </summary>
    class Say : ResponseBase
    {
        private readonly string _target;
        private readonly string _payload;

        public Say(ConfigItem rule, string cmdline) : base(rule, cmdline)
        {
            var line = cmdline.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length != 3) throw new RuleImportException("Incorrect number of parameters.");

            // Very basic target verification. Could be improved?
            if (line[1][0] != '@' && line[1][0] != '#')
                throw new RuleImportException("The given target is not valid.");
            _target = line[1];

            _payload = line[2];
            if (string.IsNullOrWhiteSpace(_payload))
                throw new RuleImportException("Message parameter is blank or missing.");
        }

        public override async Task Invoke(SocketMessage msg)
        {
            // ﻿ＣＨＡＮＧＥ  ＴＨＥ  ＳＡＹ
            string reply = ProcessText(_payload, msg);

            var target = await GetMessageTargetAsync(_target, msg);
            if (target == null)
            {
                await Log("Error: Unable to resolve the given target.");
            }
            await target.SendMessageAsync(reply);
        }
    }
}
