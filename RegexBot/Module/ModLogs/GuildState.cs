using Discord;
using Discord.Webhook;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// ModLogs guild-specific values.
    /// </summary>
    class GuildState
    {
        // Event reporting
        private DiscordWebhookClient _rptTarget;
        private LogEntry.LogType _rptTypes;
        private ulong _rptIgnore;
        /// <summary>
        /// Webhook for log reporting.
        /// </summary>
        public DiscordWebhookClient RptTarget => _rptTarget;
        /// <summary>
        /// Event types to send to the reporting channel.
        /// </summary>
        public LogEntry.LogType RptTypes => _rptTypes;
        /// <summary>
        /// Channel for AutoReporting to ignore.
        /// </summary>
        public ulong RptIgnore => _rptIgnore;

        // Query command
        private readonly string _qCmd; // command name
        private readonly EntityList _qAccess; // list of those able to issue the command
        private readonly LogEntry.LogType _qDefaultAnswer; // default entry types to display
        /// <summary>
        /// Query command. The first word in an incoming message, including prefix, that triggers a query.
        /// </summary>
        public string QrCommand => _qCmd;
        /// <summary>
        /// List of users permitted to invoke the query command.
        /// If null, refer to the guild's Moderators list.
        /// </summary>
        public EntityList QrPermittedUsers => _qAccess;
        /// <summary>
        /// Event types to display in a query.
        /// </summary>
        public LogEntry.LogType QrTypes => _qDefaultAnswer;

        public GuildState(JObject cfgRoot)
        {
            // AutoReporting settings
            var arcfg = cfgRoot["AutoReporting"];
            if (arcfg == null)
            {
                _rptTarget = null;
                _rptTypes = LogEntry.LogType.None;
                _rptIgnore = 0;
            }
            else if (arcfg.Type == JTokenType.Object)
            {
                string whurl = arcfg["WebhookUrl"]?.Value<string>();
                if (whurl == null) throw new RuleImportException("Webhook URL for log reporting is not specified.");
                var wrx = WebhookUrlParts.Match(whurl);
                if (!wrx.Success) throw new RuleImportException("Webhook URL for log reporting is not valid.");
                var wid = ulong.Parse(wrx.Groups[1].Value);
                var wtk = wrx.Groups[2].Value;
                _rptTarget = new DiscordWebhookClient(wid, wtk,
                    new Discord.Rest.DiscordRestConfig() { DefaultRetryMode = RetryMode.RetryRatelimit });
                // TODO figure out how to hook up the webhook client's log event

                // TODO make optional
                string rpval = arcfg["Events"]?.Value<string>();
                try
                {
                    _rptTypes = LogEntry.GetLogTypeFromString(rpval);
                }
                catch (ArgumentException ex)
                {
                    throw new RuleImportException(ex.Message);
                }

                var ignoreId = arcfg["CacheIgnore"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(ignoreId)) _rptIgnore = 0;
                else if (!ulong.TryParse(ignoreId, out _rptIgnore))
                {
                    throw new RuleImportException("CacheIgnore was not set to a valid channel ID.");
                }
            }
            else
            {
                throw new RuleImportException("Section for AutoReporting is not correctly defined.");
            }
            
            // QueryCommand settings
            var qccfg = cfgRoot["QueryCommand"];
            if (qccfg == null)
            {
                _qCmd = null;
                _qAccess = null;
                _qDefaultAnswer = LogEntry.LogType.None;
            }
            else if (arcfg.Type == JTokenType.Object)
            {
                _qCmd = arcfg["Command"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(_qCmd))
                    throw new RuleImportException("Query command option must have a value.");
                if (_qCmd.Contains(" "))
                    throw new RuleImportException("Query command must not contain spaces.");

                var acl = arcfg["AllowedUsers"];
                if (acl == null) _qAccess = null;
                else _qAccess = new EntityList(acl);

                // TODO make optional
                string ansval = arcfg["DefaultEvents"]?.Value<string>();
                try
                {
                    _qDefaultAnswer = LogEntry.GetLogTypeFromString(ansval);
                }
                catch (ArgumentException ex)
                {
                    throw new RuleImportException(ex.Message);
                }

            }
            else
            {
                throw new RuleImportException("Section for QueryCommand is not correctly defined.");
            }
        }

        private static Regex WebhookUrlParts =
            new Regex(@"https?:\/\/discordapp.com\/api\/webhooks\/(\d+)\/([^/]+)?", RegexOptions.Compiled);
    }
}
