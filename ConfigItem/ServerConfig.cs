using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Noikoio.RegexBot.ConfigItem
{
    /// <summary>
    /// Represents known information about a Discord guild (server) and other associated data
    /// </summary>
    class ServerConfig
    {
        private readonly string _name;
        private ulong? _id;
        private EntityList _moderators;
        private ReadOnlyDictionary<BotFeature, object> _featureData;

        public string Name => _name;
        public ulong? Id {
            get => _id; set { if (!_id.HasValue) _id = value; }
        }
        public EntityList Moderators => _moderators;
        public ReadOnlyDictionary<BotFeature, object> FeatureConfigs => _featureData;

        public ServerConfig(string name, ulong? id, EntityList moderators, ReadOnlyDictionary<BotFeature, object> featureconf)
        {
            _name = name;
            _id = id;
            _moderators = moderators;
            _featureData = featureconf;
            Debug.Assert(_name != null && _moderators != null);
        }
    }
}
