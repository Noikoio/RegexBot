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
        private ReadOnlyDictionary<BotFeature, object> _featureData;

        public ulong? Id => _id;
        public EntityList Moderators => _moderators;
        public ReadOnlyDictionary<BotFeature, object> FeatureConfigs => _featureData;

        public ServerConfig(ulong id, EntityList moderators, ReadOnlyDictionary<BotFeature, object> featureconf)
        {
            _id = id;
            _moderators = moderators;
            _featureData = featureconf;
            Debug.Assert(_moderators != null && _featureData != null);
        }
    }
}
