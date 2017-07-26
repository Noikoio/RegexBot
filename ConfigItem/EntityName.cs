using System;

namespace Noikoio.RegexBot.ConfigItem
{
    enum EntityType { Channel, Role, User }
    
    /// <summary>
    /// Used to join together an entity ID and its name, particularly when read from configuration.
    /// In the event of an unknown ID, the ID is found and cached. The ID should preferably be used
    /// over the entity's string-based name, as it can change at any time.
    /// In configuration, entities are fully specified with a prefix (if necessary), an ID, two colons, and a name.
    /// </summary>
    internal class EntityName
    {
        private ulong? _id;
        private string _name;
        private readonly EntityType _type;

        public ulong? Id => _id;
        public string Name => _name;
        public EntityType Type => _type;

        /// <summary>
        /// Creates a new EntityItem instance
        /// </summary>
        /// <param name="input">Input text WITHOUT the leading prefix. It must be stripped beforehand.</param>
        /// <param name="t">Type of this entity. Should be determined by the input prefix.</param>
        public EntityName(string input, EntityType t)
        {
            _type = t;

            // Check if input contains both ID and label
            int separator = input.IndexOf("::");
            if (separator != -1)
            {
                _name = input.Substring(separator + 2, input.Length - (separator + 2));
                if (ulong.TryParse(input.Substring(0, separator), out var id))
                {
                    _id = id;
                }
                else
                {
                    // Failed to parse ID. Assuming the actual name includes our separator.
                    _id = null;
                    _name = input;
                }
            }
            else
            {
                // Input is either only an ID or only a name
                if (ulong.TryParse(input, out var id))
                {
                    _id = id;
                    _name = null;
                }
                else
                {
                    _name = input;
                    _id = null;
                }
            }
        }
        
        /// <summary>
        /// Updates this entity's ID value only if it was previously unknown.
        /// Additionally logs a message suggesting to insert the ID into configuration.
        /// </summary>
        public void UpdateId(ulong id)
        {
            // TODO not in here, but references to this have a lot of boilerplate around it. how to fix?
            if (_id.HasValue) return;
            _id = id;

            var log = Logger.GetLogger(Configuration.LogPrefix);
            var thisstr = this.ToString();
            log(String.Format(
                "Suggestion: \"{0}\" may be written in configuration as \"{1}\"",
                (Type == EntityType.Role ? "" : thisstr.Substring(0, 1)) + Name, thisstr));
        }

        public override string ToString()
        {
            string prefix;
            if (_type == EntityType.Channel) prefix = "#";
            else if (_type == EntityType.User) prefix = "@";
            else prefix = "";

            if (_id.HasValue && _name != null)
            {
                return $"{prefix}{Id}::{Name}";
            }

            if (_id.HasValue)
            {
                return $"{prefix}{Id}";
            }

            return $"{prefix}{Name}";
        }
    }
}
