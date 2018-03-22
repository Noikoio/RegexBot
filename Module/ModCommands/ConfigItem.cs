using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using Noikoio.RegexBot.Module.ModCommands.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Noikoio.RegexBot.Module.ModCommands
{
    /// <summary>
    /// Contains a server's ModCommands configuration.
    /// </summary>
    class ConfigItem
    {
        private readonly ReadOnlyDictionary<string, Command> _cmdInstances;
        
        public ReadOnlyDictionary<string, Command> Commands => _cmdInstances;

        public ConfigItem(ModCommands instance, JToken inconf)
        {
            if (inconf.Type != JTokenType.Object)
            {
                throw new RuleImportException("Configuration for this section is invalid.");
            }
            
            // Command instance creation
            var commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in inconf.Children<JProperty>())
            {
                string label = def.Name;
                var cmd = Command.CreateInstance(instance, def);
                if (commands.ContainsKey(cmd.Trigger))
                    throw new RuleImportException(
                        $"{label}: 'command' value must not be equal to that of another definition. " +
                        $"Given value is being used for \"{commands[cmd.Trigger].Label}\".");

                commands.Add(cmd.Trigger, cmd);
            }
            _cmdInstances = new ReadOnlyDictionary<string, Command>(commands);
        }
    }
}
