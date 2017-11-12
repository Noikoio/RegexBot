using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Noikoio.RegexBot.ConfigItem
{
    /// <summary>
    /// Represents known information about a Discord guild (server) and other associated data
    /// </summary>
    class ServerConfig
    {
        private readonly ulong _id;
        private EntityList _moderators;
        private ReadOnlyDictionary<BotModule, object> _modData;

        public ulong? Id => _id;
        public EntityList Moderators => _moderators;
        public ReadOnlyDictionary<BotModule, object> ModuleConfigs => _modData;

        public ServerConfig(ulong id, EntityList moderators, ReadOnlyDictionary<BotModule, object> modconf)
        {
            _id = id;
            _moderators = moderators;
            _modData = modconf;
            Debug.Assert(_moderators != null && _modData != null);
        }
    }
}
