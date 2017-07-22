using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Noikoio.RegexBot.ConfigItem
{
    enum FilterType { None, Whitelist, Blacklist }

    /// <summary>
    /// Represents a structure in bot configuration that contains a list of
    /// channels, roles, and users.
    /// </summary>
    class EntityList
    {
        private readonly Dictionary<EntityType, EntityName[]> _innerList;

        public IEnumerable<EntityName> Channels => _innerList[EntityType.Channel];
        public IEnumerable<EntityName> Roles => _innerList[EntityType.Role];
        public IEnumerable<EntityName> Users => _innerList[EntityType.User];

        public EntityList() : this(null) { }
        public EntityList(JToken config)
        {
            _innerList = new Dictionary<EntityType, EntityName[]>();
            if (config == null)
            {
                foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
                {
                    _innerList.Add(t, new EntityName[0]);
                }
            }
            else
            {
                foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
                {
                    string aname = Enum.GetName(typeof(EntityType), t).ToLower() + "s";
                    List<EntityName> items = new List<EntityName>();

                    JToken array = config[aname];
                    if (array != null)
                    {
                        foreach (var item in array) {
                            string input = item.Value<string>();
                            if (t == EntityType.User && input.StartsWith("@")) input = input.Substring(1);
                            if (t == EntityType.Channel && input.StartsWith("#")) input = input.Substring(1);
                            if (input.Length > 0) items.Add(new EntityName(input, t));
                        }
                    }

                    _innerList.Add(t, items.ToArray());
                }
            }
            Debug.Assert(Channels != null && Roles != null && Users != null);
        }

        public override string ToString()
        {
            return $"List contains: "
                + $"{Channels.Count()} channel(s), "
                + $"{Roles.Count()} role(s), "
                + $"{Users.Count()} user(s)";
        }


        /// <summary>
        /// Helper method for reading whitelist and blacklist filtering lists
        /// </summary>
        public static (FilterType, EntityList) GetFilterList(JObject section)
        {
            var mode = FilterType.None;
            EntityList list;
            if (section["whitelist"] != null) mode = FilterType.Whitelist;
            if (section["blacklist"] != null)
            {
                if (mode == FilterType.Whitelist)
                    throw new RuleConfig.RuleImportException("Cannot have whitelist AND blacklist defined.");
                mode = FilterType.Blacklist;
            }
            if (mode == FilterType.None) list = new EntityList(); // might even be fine to keep it null?
            else list = new EntityList(section[mode == FilterType.Whitelist ? "whitelist" : "blacklist"]);

            return (mode, list);
        }
    }
}
