using Discord.WebSocket;
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
        /// Checks if the parameters of the given <see cref="SocketMessage"/> match with the entities
        /// specified in this list.
        /// </summary>
        /// <param name="msg">An incoming message.</param>
        /// <returns>
        /// True if the <see cref="SocketMessage"/> occurred within a channel specified in this list,
        /// or if the message author belongs to one or more roles in this list, or if the user itself
        /// is defined within this list.
        /// </returns>
        public bool ExistsInList(SocketMessage msg)
        {
            var guildauthor = msg.Author as SocketGuildUser;
            foreach (var item in this.Users)
            {
                if (!item.Id.HasValue)
                {
                    if (guildauthor != null &&
                        string.Equals(item.Name, guildauthor.Nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (string.Equals(item.Name, msg.Author.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else
                {
                    if (item.Id.Value == msg.Author.Id) return true;
                }
            }

            if (guildauthor != null)
            {
                foreach (var guildrole in guildauthor.Roles)
                {
                    if (this.Roles.Any(listrole =>
                    {
                        if (listrole.Id.HasValue) return listrole.Id == guildrole.Id;
                        else return string.Equals(listrole.Name, guildrole.Name, StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        return true;
                    }
                }

                foreach (var listchannel in this.Channels)
                {
                    if (listchannel.Id.HasValue && listchannel.Id == msg.Channel.Id ||
                        string.Equals(listchannel.Name, msg.Channel.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // No match.
            return false;
        }

        /// <summary>
        /// Helper method for reading whitelist and blacklist filtering lists.
        /// </summary>
        /// <param name="section">
        /// A JSON object which presumably contains an array named "whitelist" or "blacklist".
        /// </param>
        public static (FilterType, EntityList) GetFilterList(JObject section)
        {
            var mode = FilterType.None;
            EntityList list;
            if (section["whitelist"] != null) mode = FilterType.Whitelist;
            if (section["blacklist"] != null)
            {
                if (mode == FilterType.Whitelist)
                    throw new RuleImportException("Cannot have whitelist AND blacklist defined.");
                mode = FilterType.Blacklist;
            }
            if (mode == FilterType.None) list = new EntityList(); // might even be fine to keep it null?
            else list = new EntityList(section[mode == FilterType.Whitelist ? "whitelist" : "blacklist"]);

            return (mode, list);
        }
    }
}
