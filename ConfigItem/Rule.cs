using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.ConfigItem
{
    /// <summary>
    /// Represents configuration for a single rule.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("Rule: {DisplayName}")]
    internal struct Rule
    {
        private string _displayName;
        private Server _server;
        private IEnumerable<Regex> _regex;
        private IEnumerable<string> _responses;
        private FilterType _filtermode;
        private EntityList _filterlist;
        private EntityList _filterexempt;
        private int? _minLength;
        private int? _maxLength;
        private bool _modBypass;
        private bool _matchEmbeds;

        public string DisplayName => _displayName;
        public Server Server => _server;
        public IEnumerable<Regex> Regex => _regex;
        public IEnumerable<string> Responses => _responses;
        public FilterType FilterMode => _filtermode;
        public EntityList FilterList => _filterlist;
        public EntityList FilterExemptions => _filterexempt;
        public int? MinLength => _minLength;
        public int? MaxLength => _maxLength;
        public bool AllowModBypass => _modBypass;
        public bool MatchEmbeds => _matchEmbeds;

        /// <summary>
        /// Takes the JObject for a single rule and retrieves all data for use as a struct.
        /// </summary>
        /// <param name="ruleconf">Rule configuration input</param>
        /// <exception cref="RuleImportException>">
        /// Thrown when encountering a missing or invalid value.
        /// </exception>
        public Rule(Server serverref, JObject ruleconf)
        {
            _server = serverref;

            // display name - validation should've been done outside this constructor already
            _displayName = ruleconf["name"]?.Value<string>();
            if (_displayName == null)
                throw new RuleImportException("Display name not defined.");

            // regex options
            RegexOptions opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            // TODO consider adding an option to specify Singleline and Multiline matching
            opts |= RegexOptions.Singleline;
            // case sensitivity must be explicitly defined, else not case sensitive by default
            bool? regexci = ruleconf["ignorecase"]?.Value<bool>();
            opts |= RegexOptions.IgnoreCase;
            if (regexci.HasValue && regexci.Value == false)
                opts &= ~RegexOptions.IgnoreCase;

            // regex
            const string RegexError = "No regular expression patterns are defined.";
            var regexes = new List<Regex>();
            var rxconf = ruleconf["regex"];
            if (rxconf == null)
            {
                throw new RuleImportException(RegexError);
            }
            if (rxconf.Type == JTokenType.Array)
            {
                foreach (var input in rxconf.Values<string>())
                {
                    try
                    {
                        Regex r = new Regex(input, opts);
                        regexes.Add(r);
                    }
                    catch (ArgumentException)
                    {
                        throw new RuleImportException("Failed to parse regular expression pattern: " + input);
                    }
                }
            }
            else
            {
                string rxstr = rxconf.Value<string>();
                try
                {
                    var rxx = new Regex(rxstr, opts);
                    regexes.Add(rxx);
                }
                catch (ArgumentException)
                {
                    throw new RuleImportException("Failed to parse regular expression pattern: " + rxstr);
                }
            }
            if (regexes.Count == 0)
            {
                throw new RuleImportException(RegexError);
            }
            _regex = regexes.ToArray();

            // min/max length
            try
            {
                _minLength = ruleconf["min"]?.Value<int>();
                _maxLength = ruleconf["max"]?.Value<int>();
            }
            catch (FormatException)
            {
                throw new RuleImportException("Minimum/maximum values must be an integer.");
            }

            // responses
            const string ResponseError = "No responses have been defined for this rule.";
            var responses = new List<string>();
            var rsconf = ruleconf["response"];
            if (rsconf == null)
            {
                throw new RuleImportException(ResponseError);
            }
            if (rsconf.Type == JTokenType.Array)
            {
                foreach (var input in rsconf.Values<string>()) responses.Add(input);
            }
            else
            {
                responses.Add(rsconf.Value<string>());
            }
            // TODO a bit of response validation here. at least check for blanks or something.
            _responses = responses.ToArray();

            // (white|black)list filtering
            (_filtermode, _filterlist) = EntityList.GetFilterList(ruleconf);
            // filtering exemptions
            _filterexempt = new EntityList(ruleconf["exempt"]);

            // moderator bypass toggle - true by default, must be explicitly set to false
            bool? modoverride = ruleconf["AllowModBypass"]?.Value<bool>();
            _modBypass = modoverride.HasValue ? modoverride.Value : true;

            // embed matching mode
            bool? embedmode = ruleconf["MatchEmbeds"]?.Value<bool>();
            _matchEmbeds = (embedmode.HasValue && embedmode == true);
        }

        /// <summary>
        /// Exception thrown during an attempt to read rule configuration.
        /// </summary>
        public class RuleImportException : Exception
        {
            public RuleImportException(string message) : base(message) { }
        }
    }
    
    
}
