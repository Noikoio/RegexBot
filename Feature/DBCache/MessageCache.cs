using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.DBCaches
{
    /// <summary>
    /// Caches information regarding all incoming messages.
    /// The function of this feature should be transparent to the user, and thus no configuration is needed.
    /// </summary>
    class MessageCache : BotFeature
    {
        private readonly DatabaseConfig _db;

        public override string Name => nameof(MessageCache);

        #region Table setup
        const string TableGuild = "cache_guild";
        const string TableUser = "cache_users";
        const string TableMessage = "cache_messages";

        public MessageCache(DiscordSocketClient client) : base(client)
        {
            _db = RegexBot.Config.Database;

            client.MessageReceived += Client_MessageReceived;
            //client.MessageUpdated += Client_MessageUpdated;
        }

        public override Task<object> ProcessConfiguration(JToken configSection) => Task.FromResult<object>(null);

        #region Event handling
        // A new message has been created
        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (!_db.Enabled) return;
            throw new NotImplementedException();
        }
        
        //private Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        /*
         * Edited messages seem to retain their ID. This is a problem.
         * The point of this message cache was to have another feature be able to relay
         * both the previous and current message at once.
         * For now: Do nothing on updated messages.
        */
        #endregion

        private async Task CreateCacheTables(ulong gid)
        {
            /* Note:
             * We save information per guild in their own schemas named "g_NUM", where NUM is the Guild ID.
             * 
             * The creation of these schemas is handled within here, but we're possibly facing a short delay
             * in the event that other events that we're listening for come in without a schema having been
             * created yet in which to put them in.
             * Got to figure that out.
             */
            await _db.CreateGuildSchemaAsync(gid);

            using (var db = await _db.OpenConnectionAsync(gid))
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableMessage + "("
                        + "snowflake bigint primary key, "
                        + "cache_date timestamptz not null, "
                        + "author bigint not null"
                        + ")";
                    await c.ExecuteNonQueryAsync();
                }
            }
        }
        #endregion

        private async Task CacheMessage(SocketMessage msg)
        {
            throw new NotImplementedException();
        }

        private async Task UpdateMessage(SocketMessage msg)
        {
            throw new NotImplementedException();
        }
    }
}
