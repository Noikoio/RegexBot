using System.Collections.Generic;
using System.Diagnostics;

namespace Noikoio.RegexBot.ConfigItem
{
    /// <summary>
    /// Represents known information about a Discord guild (server) and other associated data
    /// </summary>
    class Server
    {
        private readonly string _name;
        private ulong? _id;
        private IEnumerable<Rule> _rules;
        private EntityList _moderators;

        public string Name => _name;
        public ulong? Id {
            get => _id; set { if (!_id.HasValue) _id = value; }
        }
        public IEnumerable<Rule> MatchResponseRules => _rules;
        public EntityList Moderators => _moderators;

        public Server(string name, ulong? id, IEnumerable<Rule> rules, EntityList moderators)
        {
            _name = name;
            _id = id;
            _rules = rules;
            _moderators = moderators;
            Debug.Assert(_name != null && _rules != null && _moderators != null);
        }
    }
}
