using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;

namespace Noikoio.RegexBot.Module.EntryAutoRole
{
    class ModuleConfig
    {
        private EntityName _cfgRole;
        private int _cfgTime;

        public EntityName Role => _cfgRole;
        public int TimeDelay => _cfgTime;

        public ModuleConfig(JObject conf)
        {
            var cfgRole = conf["Role"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(cfgRole))
                throw new RuleImportException("Role was not specified.");
            _cfgRole = new EntityName(cfgRole, EntityType.Role);

            var inTime = conf["WaitTime"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(inTime))
                throw new RuleImportException("WaitTime was not specified.");

            if (!int.TryParse(inTime, out _cfgTime))
            {
                throw new RuleImportException("WaitTime must be a numeric value.");
            }
            if (_cfgTime < 0)
            {
                throw new RuleImportException("WaitTime must be a positive integer.");
            }
        }
    }
}
