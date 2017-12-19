using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Noikoio.RegexBot.Module.ModTools
{
    /// <summary>
    /// Represents ModTools configuration within one server.
    /// </summary>
    class ConfigItem
    {
        private readonly ReadOnlyDictionary<string, CommandBase> _cmdInstances;
        
        public ReadOnlyDictionary<string, CommandBase> Commands => _cmdInstances;

        public ConfigItem(ModTools instance, JToken inconf)
        {
            if (inconf.Type != JTokenType.Object)
            {
                throw new RuleImportException("Configuration for this section is invalid.");
            }
            var config = (JObject)inconf;
            

            // Command instances
            var commands = new Dictionary<string, CommandBase>(StringComparer.OrdinalIgnoreCase);
            var commandconf = config["Commands"];
            if (commandconf != null)
            {
                if (commandconf.Type != JTokenType.Object)
                {
                    throw new RuleImportException("CommandDefs is not properly defined.");
                }

                foreach (var def in commandconf.Children<JProperty>())
                {
                    string label = def.Name;
                    var cmd = CommandBase.CreateInstance(instance, def);
                    if (commands.ContainsKey(cmd.Command))
                        throw new RuleImportException(
                            $"{label}: 'command' value must not be equal to that of another definition. " +
                            $"Given value is being used for {commands[cmd.Command].Label}.");

                    commands.Add(cmd.Command, cmd);
                }
            }
            _cmdInstances = new ReadOnlyDictionary<string, CommandBase>(commands);
        }
    }
}
