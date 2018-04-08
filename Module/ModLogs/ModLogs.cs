using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Logs certain events of note to a database for moderators to keep track of user behavior.
    /// Makes use of a helper class, <see cref="MessageCache"/>.
    /// </summary>
    class ModLogs : BotModule
    {
        private readonly MessageCache _msgCacheInstance;

        public ModLogs(DiscordSocketClient client) : base(client)
        {
            // Do nothing if database unavailable. The user will be informed by ProcessConfiguration.
            if (!RegexBot.Config.DatabaseAvailable) return;
            
            // MessageCache (reporting of MessageEdit, MessageDelete) handled by helper class
            _msgCacheInstance = new MessageCache(client, Log, delegate (ulong id) { return GetState<GuildState>(id); });

            LogEntry.CreateTable();

            // TODO add handlers for detecting joins, leaves, bans, kicks, user edits (nick/username/discr)
            // TODO add handler for processing the log query command
        }
        
        public override async Task<object> CreateInstanceState(JToken configSection)
        {
            if (configSection == null) return null;
            if (configSection.Type != JTokenType.Object)
                throw new RuleImportException("Configuration for this section is invalid.");
            
            if (!RegexBot.Config.DatabaseAvailable)
            {
                await Log("Database access is not available. This module be unavailable.");
                return null;
            }

            var conf = new GuildState((JObject)configSection);
            if (conf.RptTypes != LogEntry.LogType.None) await Log("Enabled event autoreporting.");

            return conf;
        }
    }
}