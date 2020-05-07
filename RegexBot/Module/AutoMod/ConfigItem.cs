using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod
{
    /// <summary>
    /// Representation of a single AutoMod rule.
    /// Data stored within cannot be edited.
    /// </summary>
    class ConfigItem
    {
        readonly AutoMod _instance;
        readonly string _label;
        readonly IEnumerable<Regex> _regex;
        readonly ICollection<ResponseBase> _responses;
        readonly FilterList _filter;
        readonly int _msgMinLength;
        readonly int _msgMaxLength;
        readonly bool _modBypass;
        readonly bool _embedMode;

        public string Label => _label;
        public IEnumerable<Regex> Regex => _regex;
        public ICollection<ResponseBase> Response => _responses;
        public FilterList Filter => _filter;
        public (int?, int?) MatchLengthMinMaxLimit => (_msgMinLength, _msgMaxLength);
        public bool AllowsModBypass => _modBypass;
        public bool MatchEmbed => _embedMode;

        public DiscordSocketClient Discord => _instance.Client;
        public Func<string, Task> Logger => _instance.Log;

        /// <summary>
        /// Creates a new Rule instance to represent the given configuration.
        /// </summary>
        public ConfigItem(AutoMod instance, JProperty definition)
        {
            _instance = instance;

            _label = definition.Name;
            var ruleconf = (JObject)definition.Value;
            // TODO validation. does the above line even throw an exception in the right cases?
            // and what about the label? does it make for a good name?

            string errpfx = $" in definition for rule '{_label}'.";

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
            const string NoRegexError = "No regular expression patterns are defined";
            var regexes = new List<Regex>();
            var rxconf = ruleconf["regex"];
            if (rxconf == null) throw new RuleImportException(NoRegexError + errpfx);
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
                        throw new RuleImportException(
                            $"Failed to parse regular expression pattern '{input}'{errpfx}");
                    }
                }
            }
            else
            {
                string rxstr = rxconf.Value<string>();
                try
                {
                    Regex r = new Regex(rxstr, opts);
                    regexes.Add(r);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    throw new RuleImportException(
                        $"Failed to parse regular expression pattern '{rxstr}'{errpfx}");
                }
            }
            if (regexes.Count == 0)
            {
                throw new RuleImportException(NoRegexError + errpfx);
            }
            _regex = regexes.ToArray();

            // min/max length
            try
            {
                _msgMinLength = ruleconf["min"]?.Value<int>() ?? -1;
                _msgMaxLength = ruleconf["max"]?.Value<int>() ?? -1;
            }
            catch (FormatException)
            {
                throw new RuleImportException("Minimum/maximum values must be an integer.");
            }

            // responses
            const string NoResponseError = "No responses have been defined";
            var responsestrs = new List<string>();
            var rsconf = ruleconf["response"];
            if (rsconf == null) throw new RuleImportException(NoResponseError + errpfx);
            try
            {
                if (rsconf.Type == JTokenType.Array)
                {
                    _responses = ResponseBase.ReadConfiguration(this, rsconf.Values<string>());
                }
                else
                {
                    _responses = ResponseBase.ReadConfiguration(this, new string[] { rsconf.Value<string>() });
                }
            }
            catch (RuleImportException ex)
            {
                throw new RuleImportException(ex.Message + errpfx);
            }
            

            // whitelist/blacklist filtering
            _filter = new FilterList(ruleconf);

            // moderator bypass toggle - true by default, must be explicitly set to false
            bool? bypass = ruleconf["AllowModBypass"]?.Value<bool>();
            _modBypass = bypass.HasValue ? bypass.Value : true;

            // embed matching mode
            bool? embed = ruleconf["MatchEmbeds"]?.Value<bool>();
            _embedMode = (embed.HasValue && embed == true);
        }

        /// <summary>
        /// Checks given message to see if it matches this rule's constraints.
        /// </summary>
        /// <returns>If true, the rule's response(s) should be executed.</returns>
        public bool Match(SocketMessage m, bool isMod)
        {
            // Regular or embed mode?
            string msgcontent;
            if (MatchEmbed) msgcontent = SerializeEmbed(m.Embeds);
            else msgcontent = m.Content;
            if (msgcontent == null) return false;

            // Min/max length check
            if (_msgMinLength != -1 && msgcontent.Length <= _msgMinLength) return false;
            if (_msgMaxLength != -1 && msgcontent.Length >= _msgMaxLength) return false;

            // Filter check
            if (Filter.IsFiltered(m)) return false;
            // Mod bypass check
            if (AllowsModBypass && isMod) return false;

            // Finally, regex checks
            foreach (var regex in Regex)
            {
                if (regex.IsMatch(msgcontent)) return true;
            }
            return false;
        }
        
        private string SerializeEmbed(IReadOnlyCollection<Embed> e)
        {
            var text = new StringBuilder();
            foreach (var item in e) text.AppendLine(SerializeEmbed(item));
            return text.ToString();
        }

        /// <summary>
        /// Converts an embed to a plain string for easier matching.
        /// </summary>
        private string SerializeEmbed(Embed e)
        {
            StringBuilder result = new StringBuilder();
            if (e.Author.HasValue) result.AppendLine(e.Author.Value.Name ?? "" + e.Author.Value.Url ?? "");

            if (!string.IsNullOrWhiteSpace(e.Title)) result.AppendLine(e.Title);
            if (!string.IsNullOrWhiteSpace(e.Description)) result.AppendLine(e.Description);

            foreach (var f in e.Fields)
            {
                if (!string.IsNullOrWhiteSpace(f.Name)) result.AppendLine(f.Name);
                if (!string.IsNullOrWhiteSpace(f.Value)) result.AppendLine(f.Value);
            }

            if (e.Footer.HasValue)
            {
                result.AppendLine(e.Footer.Value.Text ?? "");
            }

            return result.ToString();
        }

        public override string ToString() => $"Rule '{Label}'";
    }
}
