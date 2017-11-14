using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using Npgsql;
using NpgsqlTypes;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.DBCache
{
    /// <summary>
    /// Caches information regarding all incoming messages.
    /// The function of this feature should be transparent to the user, and thus no configuration is needed.
    /// </summary>
    class MessageCache : BotFeature
    {
        // TODO Something that clears expired cache items
        private readonly DatabaseConfig _db;

        public override string Name => nameof(MessageCache);

        public MessageCache(DiscordSocketClient client) : base(client)
        {
            _db = RegexBot.Config.Database;

            if (_db.Enabled)
            {
                CreateCacheTables();

                client.MessageReceived += Client_MessageReceived;
                //client.MessageUpdated += Client_MessageUpdated;
            }
            else
            {
                Log("No database storage available.").Wait();
            }
        }

        #region Table setup
        const string TableMessage = "cache_messages";
        
        public override Task<object> ProcessConfiguration(JToken configSection) => Task.FromResult<object>(null);

        #region Event handling
        // A new message has been created
        private async Task Client_MessageReceived(SocketMessage arg)
        {
            await Task.Run(() => CacheMessage(arg));
        }
        
        //private Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        /*
         * Edited messages seem to retain their ID. This is a problem.
         * The point of this message cache was to have another feature be able to relay
         * both the previous and current message at once.
         * For now: Do nothing on updated messages.
        */
        #endregion

        private void CreateCacheTables()
        {
            using (var db = _db.GetOpenConnectionAsync().GetAwaiter().GetResult())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableMessage + " ("
                        + "message_id bigint primary key, "
                        + "author_id bigint not null, "
                        + "guild_id bigint not null, "
                        + "channel_id bigint not null, " // channel cache later? something to think about...
                        + "created_ts timestamptz not null, "
                        + "edited_ts timestamptz null, "
                        + "message text not null, "
                        + $"FOREIGN KEY (author_id, guild_id) references {EntityCache.Sql.TableUser} (user_id, guild_id)"
                        + ")";
                    // TODO figure out how to store message edits
                    c.ExecuteNonQuery();
                }
            }
        }
        #endregion

        private async Task CacheMessage(SocketMessage msg)
        {
            try
            {
                using (var db = await _db.GetOpenConnectionAsync())
                {
                    using (var c = db.CreateCommand())
                    {
                        c.CommandText = "INSERT INTO " + TableMessage
                            + " (message_id, author_id, guild_id, channel_id, created_ts, message) VALUES "
                            + "(@MessageId, @UserId, @GuildId, @ChannelId, @Date, @Message)";
                        c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = msg.Id;
                        c.Parameters.Add("@UserId", NpgsqlDbType.Bigint).Value = msg.Author.Id;
                        c.Parameters.Add("@GuildId", NpgsqlDbType.Bigint).Value = ((SocketGuildUser)msg.Author).Guild.Id;
                        c.Parameters.Add("@ChannelId", NpgsqlDbType.Bigint).Value = msg.Channel.Id;
                        c.Parameters.Add("@Date", NpgsqlDbType.TimestampTZ).Value = msg.Timestamp;
                        c.Parameters.Add("@Message", NpgsqlDbType.Text).Value = msg.Content;
                        c.Prepare();
                        await c.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                await Log($"SQL error in {nameof(CacheMessage)}: " + ex.Message);
            }
        }
    }
}
