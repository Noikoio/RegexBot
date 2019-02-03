using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.Module.AutoRespond
{
    /// <summary>
    /// Represents a single autoresponse definition.
    /// </summary>
    class ConfigItem
    {
        public enum ResponseType { None, Exec, Reply }
        private static Random ChangeRng = new Random();

        ResponseType _rtype;
        string _rbody;
        private double _random;

        public string Label { get; }
        public IEnumerable<Regex> Regex { get; }
        public (ResponseType, string) Response => (_rtype, _rbody);
        public FilterList Filter { get; }
        public RateLimitCache RateLimit { get; }
        public double RandomChance => _random;

        public ConfigItem(JProperty definition)
        {
            Label = definition.Name;
            var data = (JObject)definition.Value;

            // error postfix string
            string errorpfx = $" in response definition for '{Label}'.";

            // regex trigger
            const string NoRegexError = "No regular expression patterns are defined";
            var regexes = new List<Regex>();
            const RegexOptions rxopts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline;
            var rxconf = data["regex"];
            if (rxconf == null) throw new RuleImportException(NoRegexError + errorpfx);
            if (rxconf.Type == JTokenType.Array)
            {
                foreach (var input in rxconf.Values<string>())
                {
                    try
                    {
                        Regex r = new Regex(input, rxopts);
                        regexes.Add(r);
                    }
                    catch (ArgumentException)
                    {
                        throw new RuleImportException(
                            $"Failed to parse regular expression pattern '{input}'{errorpfx}");
                    }
                }
            }
            else
            {
                string rxstr = rxconf.Value<string>();
                try
                {
                    Regex r = new Regex(rxstr, rxopts);
                    regexes.Add(r);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    throw new RuleImportException(
                        $"Failed to parse regular expression pattern '{rxstr}'{errorpfx}");
                }
            }
            Regex = regexes.ToArray();

            // response - defined in either "exec" or "reply", but not both
            _rbody = null;
            _rtype = ResponseType.None;

            // exec response
            string execstr = data["exec"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(execstr))
            {
                _rbody = execstr;
                _rtype = ResponseType.Exec;
            }

            // reply response
            string replystr = data["reply"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(replystr))
            {
                if (_rbody != null)
                    throw new RuleImportException("A value for both 'exec' and 'reply' is not allowed" + errorpfx);
                _rbody = replystr;
                _rtype = ResponseType.Reply;
            }

            if (_rbody == null)
                throw new RuleImportException("A response value of either 'exec' or 'reply' was not defined" + errorpfx);
            // ---

            // whitelist/blacklist filtering
            Filter = new FilterList(data);

            // rate limiting
            string rlstr = data["ratelimit"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(rlstr))
            {
                RateLimit = new RateLimitCache(RateLimitCache.DefaultTimeout);
            }
            else
            {
                if (uint.TryParse(rlstr, out var rlval))
                {
                    RateLimit = new RateLimitCache(rlval);
                }
                else
                {
                    throw new RuleImportException("Rate limit value is invalid" + errorpfx);
                }
            }

            // random chance
            string randstr = data["RandomChance"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(randstr))
            {
                _random = double.NaN;
            }
            else
            {
                if (!double.TryParse(randstr, out _random))
                {
                    throw new RuleImportException("Random value is invalid (unable to parse)" + errorpfx);
                }
                if (_random > 1 || _random < 0)
                {
                    throw new RuleImportException("Random value is invalid (not between 0 and 1)" + errorpfx);
                }
            }
        }

        /// <summary>
        /// Checks given message to see if it matches this rule's constraints.
        /// </summary>
        /// <returns>If true, the rule's response(s) should be executed.</returns>
        public bool Match(SocketMessage m)
        {
            // Filter check
            if (Filter.IsFiltered(m)) return false;

            // Match check
            bool matchFound = false;
            foreach (var item in Regex)
            {
                if (item.IsMatch(m.Content))
                {
                    matchFound = true;
                    break;
                }
            }
            if (!matchFound) return false;

            // Rate limit check - currently per channel
            if (!RateLimit.AllowUsage(m.Channel.Id)) return false;

            // Random chance check
            if (!double.IsNaN(RandomChance))
            {
                // Fail if randomly generated value is higher than the parameter
                // Example: To fail a 75% chance, the check value must be between 0.75000...001 and 1.0.
                var chk = ChangeRng.NextDouble();
                if (chk > RandomChance) return false;
            }

            return true;
        }

        public override string ToString() => $"Autoresponse definition '{Label}'";
    }
}
