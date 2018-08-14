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

            client.UserJoined += Client_UserJoined;
            client.UserLeft += Client_UserLeft;
            client.UserUpdated += Client_UserUpdated;
            client.UserBanned += Client_UserBanned;
            client.UserUnbanned += Client_UserUnbanned;
            
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

        private Task Client_UserJoined(SocketGuildUser arg)
        {
            throw new System.NotImplementedException();
        }

        private Task Client_UserLeft(SocketGuildUser arg)
        {
            throw new System.NotImplementedException();
        }
        
        private Task Client_UserUpdated(SocketUser arg1, SocketUser arg2)
        {
            throw new System.NotImplementedException();
        }

        private Task Client_UserBanned(SocketUser arg1, SocketGuild arg2)
        {
            throw new System.NotImplementedException();
        }

        private Task Client_UserUnbanned(SocketUser arg1, SocketGuild arg2)
        {
            throw new System.NotImplementedException();
        }
    }
}