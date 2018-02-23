using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Logs certain events of note to a database for moderators to keep track of user behavior.
    /// Makes use of a helper class, <see cref="MessageCache"/>.
    /// </summary>
    class ModLogs : BotModule
    {
        public override string Name => "ModLogs";

        private readonly MessageCache _msgCacheInstance;

        public ModLogs(DiscordSocketClient client) : base(client)
        {
            // Do nothing if database unavailable. The user will be informed by ProcessConfiguration.
            if (!RegexBot.Config.DatabaseAvailable) return;
            
            _msgCacheInstance = new MessageCache(client, Log, GetConfig);

            //throw new NotImplementedException();
        }

        [ConfigSection("ModLogs")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            if (configSection.Type != JTokenType.Object)
                throw new RuleImportException("Configuration for this section is invalid.");
            var conf = (JObject)configSection;
            
            if (!RegexBot.Config.DatabaseAvailable)
            {
                await Log("Database access is not available. This module be unavailable.");
                return null;
            }
            
            try
            {
                // MessageCache testing: will store an EntityName or die trying
                EntityName? mctarget = new EntityName(conf["mctarget"].Value<string>(), EntityType.Channel);
                await Log("Enabled MessageCache test on " + mctarget.Value.ToString());
                return mctarget;
            }
            catch (Exception)
            {
                // well, not really die
                return null;
            }

            /*
             * Concept:
             *  "ModLogs": {
             *      "AutoReporting": {
             *          // behavior for how to output to the reporting channel
             *          // MessageCache looks for configuration values within here.
             *          "Channel": "something compatible with EntityName",
             *          "Events": "perhaps a single string of separated event types"
             *      },
             *      "QueryOptions": {
             *          // Behavior for the query command (which is defined here rather than ModTools)
             *          // Need to stress in the documentation that "msgedit" and "msgdelete" events
             *          // are not kept and cannot be queried
             *          "QueryCommand": "!modlogs",
             *          "Permission": "Moderators", // either a string that says "Moderators" or an EntityList
             *          "DefaultQueryEvents": "another single string of separated event types",
             *      }
             *  }
             */
        }
    }
}