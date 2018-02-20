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
            if (!RegexBot.Config.DatabaseAvailable) return; // do nothing; warn in ProcessConfiguration
            
            _msgCacheInstance = new MessageCache(client, Log, GetConfig);

            throw new NotImplementedException();
        }

        [ConfigSection("ModLogs")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            if (configSection.Type != JTokenType.Object)
                throw new RuleImportException("Configuration for this section is invalid.");
            var conf = (JObject)configSection;
            
            if (!RegexBot.Config.DatabaseAvailable)
            {
                await Log("Database access is not available. This module will not load.");
                return null;
            }
            
            try
            {
                // MessageCache debug: will store an EntityName or die trying
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
             * Ideas:
             * -Reporting:
             * --Reporting channel
             * --Types to report
             * ---Ignored if no reporting channel has been set
             * ---Default to join, quit, kick, ban, ...
             * ---Any override will disregard defaults
             * -also how will commands work? how to tie into commands mod?
             * --modlogs command should also only report a subset of things. custom.
             * ---ex: don't report nick changes
             */
        }
    }
}