using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Helper class for <see cref="ModLogs"/>. Keeps a database-backed cache of recent messages and assists
    /// in reporting message changes and deletions, if configured to do so.
    /// Does not manipulate the moderation log managed by the main class, but rather provides supplemental features.
    /// </summary>
    class MessageCache
    {
        private readonly DiscordSocketClient _dClient;
        private readonly AsyncLogger _outLog;
        private readonly Func<ulong, object> _outGetConfig;

        public MessageCache(DiscordSocketClient client, AsyncLogger logger, Func<ulong, object> getConfFunc)
        {
            _dClient = client;
            _outLog = logger;
            _outGetConfig = getConfFunc;

            CreateCacheTables();

            client.MessageReceived += Client_MessageReceived;
            client.MessageUpdated += Client_MessageUpdated;
            client.MessageDeleted += Client_MessageDeleted;
        }

        #region Event handling
        private async Task Client_MessageReceived(SocketMessage arg) => await CacheMessage(arg);

        private Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            /*
             * TODO:
             * Edited messages seem to retain their ID. Need to look into this.
             * In any case, the new message must be stored in case of future edits.
             * The change must be sent to the reporting channel (if one exists) as if it were
             * a typical log entry (even though it's not).
             */
            throw new NotImplementedException();
        }

        private Task Client_MessageDeleted(Discord.Cacheable<Discord.IMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            // TODO report message deletion, if reporting channel exists and message is in cache.
            throw new NotImplementedException();
        }
        #endregion

        #region Database manipulation
        const string TableMessage = "cache_messages";

        private void CreateCacheTables()
        {
            using (var db = RegexBot.Config.GetOpenDatabaseConnectionAsync().GetAwaiter().GetResult())
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
                        + $"FOREIGN KEY (author_id, guild_id) references {EntityCache.SqlHelper.TableUser} (user_id, guild_id)"
                        + ")";
                    // TODO are more columns needed for edit info?
                    c.ExecuteNonQuery();
                }
            }
        }
        #endregion

        private async Task CacheMessage(SocketMessage msg)
        {
            try
            {
                using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
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
                await _outLog($"SQL error in {nameof(CacheMessage)}: " + ex.Message);
            }
        }
    }
}
