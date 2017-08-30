using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.Feature.AutoRespond
{
    /// <summary>
    /// Represents a single autoresponse definition.
    /// </summary>
    struct ResponseDefinition
    {
        public enum ResponseType { None, Exec, Reply }

        string _label;
        Regex _trigger;
        ResponseType _rtype;
        string _rbody; // response body
        private FilterList _filter;
        private RateLimitCache _limit;

        public string Label => _label;
        public Regex Trigger => _trigger;
        public (ResponseType, string) Response => (_rtype, _rbody);
        public FilterList Filter => _filter;
        public RateLimitCache RateLimit => _limit;

        public ResponseDefinition(JProperty definition)
        {
            _label = definition.Name;
            var data = (JObject)definition.Value;

            // error postfix string
            string errorpfx = $" in response definition for '{_label}'.";

            // regex trigger
            const RegexOptions rxopts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline;
            string triggerstr = data["trigger"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(triggerstr))
                throw new RuleImportException("Regular expression trigger is not defined" + errorpfx);
            try
            {
                _trigger = new Regex(triggerstr, rxopts);
            }
            catch (ArgumentException ex)
            {
                throw new RuleImportException
                    ("Failed to parse regular expression pattern" + errorpfx +
                    $" ({ex.GetType().Name}: {ex.Message})");
            }

            // response - defined in either "exec" or "reply", but not both
            _rbody = null;
            _rtype = ResponseType.None;

            // exec response ---
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
            _filter = new FilterList(data);

            // rate limiting
            string rlstr = data["ratelimit"].Value<string>();
            if (string.IsNullOrWhiteSpace(rlstr))
            {
                _limit = new RateLimitCache(RateLimitCache.DefaultTimeout);
            }
            else
            {
                if (ushort.TryParse(rlstr, out var rlval))
                {
                    _limit = new RateLimitCache(rlval);
                }
                else
                {
                    throw new RuleImportException("Rate limit value is invalid" + errorpfx);
                }
            }
        }
    }
}
