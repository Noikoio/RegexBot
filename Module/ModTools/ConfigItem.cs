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
        private EntityName? _petitionReportCh;
        private readonly ReadOnlyDictionary<string, CommandBase> _cmdInstances;

        public EntityName? PetitionReportingChannel => _petitionReportCh;
        public ReadOnlyDictionary<string, CommandBase> Commands => _cmdInstances;

        public ConfigItem(ModTools instance, JToken inconf)
        {
            if (inconf.Type != JTokenType.Object)
            {
                throw new RuleImportException("Configuration for this section is invalid.");
            }
            var config = (JObject)inconf;

            // Ban petition reporting channel
            var petitionstr = config["PetitionRelay"]?.Value<string>();
            if (string.IsNullOrEmpty(petitionstr)) _petitionReportCh = null;
            else if (petitionstr.Length > 1 && petitionstr[0] != '#')
            {
                // Not a channel.
                throw new RuleImportException("PetitionRelay value must be set to a channel.");
            }
            else
            {
                _petitionReportCh = new EntityName(petitionstr.Substring(1), EntityType.Channel);
            }

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

        public void UpdatePetitionChannel(ulong id)
        {
            if (!PetitionReportingChannel.HasValue) return;
            if (PetitionReportingChannel.Value.Id.HasValue) return; // nothing to update

            // For lack of a better option - create a new EntityName with ID already provided
            _petitionReportCh = new EntityName($"{id}::{PetitionReportingChannel.Value.Name}", EntityType.Channel);
        }
    }
}
