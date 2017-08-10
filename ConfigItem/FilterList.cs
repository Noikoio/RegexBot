using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Noikoio.RegexBot.ConfigItem
{
    enum FilterType { None, Whitelist, Blacklist }

    /// <summary>
    /// Represents whitelist/blacklist configuration, including exemptions.
    /// </summary>
    struct FilterList
    {
        FilterType _type;
        EntityList _filterList;
        EntityList _exemptions;

        public FilterType FilterMode => _type;
        public EntityList FilterEntities => _filterList;
        public EntityList FilterExemptions => _exemptions;

        /// <summary>
        /// Gets the 
        /// </summary>
        /// <param name="conf">
        /// A JSON object which presumably contains an array named "whitelist" or "blacklist",
        /// and optionally one named "exempt".
        /// </param>
        /// <exception cref="RuleImportException">
        /// Thrown if both "whitelist" and "blacklist" definitions were found, if
        /// "exempt" was found without a corresponding "whitelist" or "blacklist",
        /// or if there was an issue parsing an EntityList within these definitions.
        /// </exception>
        public FilterList(JObject conf)
        {
            _type = FilterType.None;

            if (conf["whitelist"] != null) _type = FilterType.Whitelist;
            if (conf["blacklist"] != null)
            {
                if (_type != FilterType.None)
                    throw new RuleImportException("Cannot have both 'whitelist' and 'blacklist' values defined.");
                _type = FilterType.Blacklist;
            }
            if (_type == FilterType.None)
            {
                _filterList = null;
                _exemptions = null;
                if (conf["exempt"] != null)
                    throw new RuleImportException("Cannot have 'exempt' defined if no corresponding " +
                        "'whitelist' or 'blacklist' has been defined in the same section.");
            }
            else
            {
                _filterList = new EntityList(conf[_type == FilterType.Whitelist ? "whitelist" : "blacklist"]);
                _exemptions = new EntityList(conf["exempt"]); // EntityList constructor checks for null value
            }
        }

        /// <summary>
        /// Determines if the parameters of '<paramref name="msg"/>' are a match with filtering
        /// rules defined in this instance.
        /// </summary>
        /// <param name="msg">An incoming message.</param>
        /// <returns>
        /// True if the user or channel specified by '<paramref name="msg"/>' is filtered by
        /// the configuration defined in this instance.
        /// </returns>
        public bool IsFiltered(SocketMessage msg)
        {
            if (FilterMode == FilterType.None) return false;

            bool inFilter = FilterEntities.ExistsInList(msg);

            if (FilterMode == FilterType.Whitelist)
            {
                if (!inFilter) return true;
                return FilterExemptions.ExistsInList(msg);
            }
            else if (FilterMode == FilterType.Blacklist)
            {
                if (!inFilter) return false;
                return !FilterExemptions.ExistsInList(msg);
            }

            throw new Exception("this shouldn't happen");
        }
    }
}
