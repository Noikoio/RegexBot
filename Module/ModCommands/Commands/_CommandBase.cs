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

namespace Noikoio.RegexBot.Module.ModCommands.Commands
{
    /// <summary>
    /// Base class for a command within the module.
    /// After implementing, don't forget to add a reference to
    /// <see cref="CreateInstance(CommandListener, JProperty)"/>.
    /// </summary>
    [DebuggerDisplay("Command def: {Label}")]
    abstract class Command
    {
        private readonly CommandListener _modtools;
        private readonly string _label;
        private readonly string _command;

        protected CommandListener Module => _modtools;
        public string Label => _label;
        public string Trigger => _command;

        public Command(CommandListener l, string label, JObject conf)
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
                { "ban",        typeof(Ban) },
                { "confreload", typeof(ConfReload) },
                { "kick",       typeof(Kick) },
                { "say",        typeof(Say) },
                { "unban",      typeof(Unban) }
            });

        public static Command CreateInstance(CommandListener root, JProperty def)
        {
            string label = def.Name;
            if (string.IsNullOrWhiteSpace(label)) throw new RuleImportException("Label cannot be blank.");

            var definition = (JObject)def.Value;
            string cmdinvoke = definition["command"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(cmdinvoke))
                throw new RuleImportException($"{label}: 'command' value was not specified.");
            if (cmdinvoke.Contains(" "))
                throw new RuleImportException($"{label}: 'command' must not contain spaces.");

            string ctypestr = definition["type"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(ctypestr))
                throw new RuleImportException($"Value 'type' must be specified in definition for '{label}'.");
            if (_commands.TryGetValue(ctypestr, out Type ctype))
            {
                try
                {
                    return (Command)Activator.CreateInstance(ctype, root, label, definition);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is RuleImportException)
                        throw new RuleImportException($"Error in configuration for command '{label}': {ex.InnerException.Message}");
                    else throw;
                }
            }
            else
            {
                throw new RuleImportException($"The given 'type' value is invalid in definition for '{label}'.");
            }
        }
        #endregion

        #region Helper methods and common values
        protected static readonly Regex UserMention = new Regex(@"<@!?(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex RoleMention = new Regex(@"<@&(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex ChannelMention = new Regex(@"<#(?<snowflake>\d+)>", RegexOptions.Compiled);
        protected static readonly Regex EmojiMatch = new Regex(@"<:(?<name>[A-Za-z0-9_]{2,}):(?<ID>\d+)>", RegexOptions.Compiled);
        #endregion
    }
}
