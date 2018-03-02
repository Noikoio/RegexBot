using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// ModLogs guild-specific configuration values.
    /// </summary>
    class GuildConfig
    {
        // Event reporting
        private readonly EntityName _rptTarget;
        private EventType _rptTypes;
        /// <summary>
        /// Target reporting channel.
        /// </summary>
        public EntityName? RptTarget => _rptTarget;
        /// <summary>
        /// Event types to send to the reporting channel.
        /// </summary>
        public EventType RptTypes => _rptTypes;

        // Query command
        private readonly string _qCmd; // command name
        private readonly EntityList _qAccess; // list of those able to issue the command
        private readonly EventType _qDefaultAnswer; // default entry types to display
        /// <summary>
        /// Query command. The first word in an incoming message, including prefix, that triggers a query.
        /// </summary>
        public string QrCommand => _qCmd;
        /// <summary>
        /// List of users permitted to invoke the query command.
        /// If null, refer to the guild's Moderators list.
        /// </summary>
        public EntityList QrPermittedUsers => _qAccess;
        /// <summary>
        /// Event types to display in a query.
        /// </summary>
        public EventType QrTypes => _qDefaultAnswer;

        public GuildConfig(JObject cfgRoot)
        {
            // AutoReporting settings
            var arcfg = cfgRoot["AutoReporting"];
            if (arcfg == null)
            {
                _rptTarget = default(EntityName); // NOTE: Change this if EntityName becomes a class later
                _rptTypes = EventType.None;
            }
            else if (arcfg.Type == JTokenType.Object)
            {
                string chval = arcfg["Channel"]?.Value<string>();
                if (chval == null) throw new RuleImportException("Reporting channel is not defined.");
                if (!string.IsNullOrWhiteSpace(chval) && chval[0] == '#')
                    _rptTarget = new EntityName(chval.Substring(1, chval.Length-1), EntityType.Channel);
                else
                    throw new RuleImportException("Reporting channel is not properly defined.");
                // Require the channel's ID for now.
                if (!_rptTarget.Id.HasValue) throw new RuleImportException("Reporting channel's ID must be specified.");

                // TODO make optional
                string rpval = arcfg["Events"]?.Value<string>();
                _rptTypes = GetTypesFromString(rpval);
            }
            else
            {
                throw new RuleImportException("Section for AutoReporting is not correctly defined.");
            }
            
            // QueryCommand settings
            var qccfg = cfgRoot["QueryCommand"];
            if (qccfg == null)
            {
                _qCmd = null;
                _qAccess = null;
                _qDefaultAnswer = EventType.None;
            }
            else if (arcfg.Type == JTokenType.Object)
            {
                _qCmd = arcfg["Command"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(_qCmd))
                    throw new RuleImportException("Query command option must have a value.");
                if (_qCmd.Contains(" "))
                    throw new RuleImportException("Query command must not contain spaces.");

                var acl = arcfg["AllowedUsers"];
                if (acl == null) _qAccess = null;
                else _qAccess = new EntityList(acl);

                // TODO make optional
                string ansval = arcfg["DefaultEvents"]?.Value<string>();
                _qDefaultAnswer = GetTypesFromString(ansval);
            }
            else
            {
                throw new RuleImportException("Section for QueryCommand is not correctly defined.");
            }
        }

        public static EventType GetTypesFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new RuleImportException("Types are not properly defined.");

            var strTypes = input.Split(
                new char[] { ' ', ',', '/', '+' }, // and more?
                StringSplitOptions.RemoveEmptyEntries);

            EventType endResult = EventType.None;
            foreach (var item in strTypes)
            {
                try
                {
                    var result = Enum.Parse<EventType>(item, true);
                    endResult |= result;
                }
                catch (ArgumentException)
                {
                    throw new RuleImportException($"Unable to determine the given event type \"{item}\"");
                }
            }

            return endResult;
        }
    }
}
