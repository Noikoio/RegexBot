using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModTools
{
    /// <summary>
    /// Base class for ModTools command.
    /// We are not using Discord.Net's Commands extension, as it does not allow for changes during runtime.
    /// </summary>
    [DebuggerDisplay("{Label}-type command")]
    abstract class CommandBase
    {
        private readonly ModTools _modtools;
        private readonly string _label;
        private readonly string _command;

        protected ModTools Mt => _modtools;
        public string Label => _label;
        public string Command => _command;

        protected CommandBase(ModTools l, string label, JObject conf)
        {
            _modtools = l;
            _label = label;
            _command = conf["command"].Value<string>();
        }

        public abstract Task Invoke(SocketGuild g, SocketMessage msg);

        protected Task Log(string text)
        {
            return _modtools.Log($"{Label}: {text}");
        }

        #region Config loading
        private static readonly ReadOnlyDictionary<string, Type> _commands =
            new ReadOnlyDictionary<string, Type>(
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                // Define all command types and their corresponding Types here
                { "ban",        typeof(Commands.Ban) },
                { "kick",       typeof(Commands.Kick) },
                { "say",        typeof(Commands.Say) }
            });

        public static CommandBase CreateInstance(ModTools root, JProperty def)
        {
            string label = def.Name;
            if (string.IsNullOrWhiteSpace(label)) throw new RuleImportException("Label cannot be blank.");

            var definition = (JObject)def.Value;
            string cmdinvoke = definition["command"].Value<string>();
            if (string.IsNullOrWhiteSpace(cmdinvoke))
                throw new RuleImportException($"{label}: 'command' value was not specified.");
            if (cmdinvoke.Contains(" "))
                throw new RuleImportException($"{label}: 'command' must not contain spaces.");

            string ctypestr = definition["type"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(ctypestr))
                throw new RuleImportException($"Value 'type' must be specified in definition for '{label}'.");
            Type ctype;
            if (!_commands.TryGetValue(ctypestr, out ctype))
                throw new RuleImportException($"The given 'type' value is invalid in definition for '{label}'.");
            
            try
            {
                return (CommandBase)Activator.CreateInstance(ctype, root, label, definition);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is RuleImportException)
                    throw new RuleImportException($"Error in configuration for command '{label}': {ex.InnerException.Message}");
                else throw;
            }
        }
        #endregion

        #region Helper methods and values
        protected static readonly Regex UserMention = new Regex(@"<@!?(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex RoleMention = new Regex(@"<@&(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex ChannelMention = new Regex(@"<#(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex EmojiMatch = new Regex(@"<:(?<name>[A-Za-z0-9_]{2,}):(?<ID>\d+)>", RegexOptions.Compiled);
        #endregion
    }
}
