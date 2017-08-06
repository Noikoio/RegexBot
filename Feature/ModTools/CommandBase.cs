using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools
{
    [DebuggerDisplay("{Label}-type command")]
    abstract class CommandBase
    {
        private readonly CommandListener _modtools;
        private readonly string _label;
        private readonly string _command;

        public static readonly Regex UserMention = new Regex(@"<@!?(?<snowflake>\d+)>", RegexOptions.Compiled);
        public static readonly Regex RoleMention = new Regex(@"<@&(?<snowflake>\d+)>", RegexOptions.Compiled);
        public static readonly Regex ChannelMention = new Regex(@"<#(?<snowflake>\d+)>", RegexOptions.Compiled);
        public static readonly Regex EmojiMatch = new Regex(@"<:(?<name>[A-Za-z0-9_]{2,}):(?<ID>\d+)>", RegexOptions.Compiled);

        public string Label => _label;
        public string Command => _command;

        protected CommandBase(CommandListener l, JObject conf)
        {
            _modtools = l;
            _label = conf["label"].Value<string>();
            if (string.IsNullOrWhiteSpace(_label))
                throw new RuleImportException("Command label is missing.");
            _command = conf["command"].Value<string>();
        }

        public abstract Task Invoke(SocketGuild g, SocketMessage msg);

        protected Task Log(string text)
        {
            return _modtools.Log($"{Label}: {text}");
        }
    }
}
