using Discord;
using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Helper class for <see cref="ModLogs"/>. Keeps a database-backed cache of recent messages for use
    /// in reporting message changes and deletions, if configured to do so.
    /// Despite its place, it does not manipulate moderation logs. It simply pulls from the same configuration.
    /// </summary>
    class MessageCache
    {
        private readonly DiscordSocketClient _dClient;
        private readonly AsyncLogger _outLog;
        private readonly Func<ulong, object> _outGetConfig;

        // TODO: How to clear the cache after a time? Can't hold on to this forever.

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
        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot) return;

            await AddOrUpdateCacheItemAsync(arg);
        }

        private async Task Client_MessageUpdated(
            Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot) return;

            // We only want channel messages
            if (after is SocketUserMessage afterMsg && !(afterMsg is IDMChannel))
            {
                if (after.Author.IsBot) return;

                // We're not interested in all message updates, only those that leave a timestamp.
                if (!afterMsg.EditedTimestamp.HasValue) return;
            }
            else return; // probably unnecessary?

            // Once an edited message is cached, the original message contents are lost.
            // This is the only time available to report it.
            await ProcessReportMessage(false, before.Id, channel, after.Content);
            
            await AddOrUpdateCacheItemAsync(after);
        }

        private async Task Client_MessageDeleted(Cacheable<Discord.IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            if (channel is IDMChannel) return; // No DMs
            await ProcessReportMessage(true, msg.Id, channel, null);
        }
        #endregion

        #region Reporting
        // Reports an edited or deleted message as if it were a log entry (even though it's not).
        private async Task ProcessReportMessage(
            bool isDelete, ulong messageId, ISocketMessageChannel ch, string editMsg)
        {
            ulong guildId;
            if (ch is SocketTextChannel sch)
            {
                if (sch is IDMChannel) return;
                guildId = sch.Guild.Id;
            }
            else return;

            // Check if this feature is enabled before doing anything else.
            var cfg = _outGetConfig(guildId) as GuildConfig;
            if (cfg == null) return;
            if (isDelete && (cfg.RptTypes & EventType.MsgDelete) == 0) return;
            if (!isDelete && (cfg.RptTypes & EventType.MsgEdit) == 0) return;

            // Ignore if it's a message being deleted withing the reporting channel.
            if (isDelete && cfg.RptTarget.Value.Id.Value == ch.Id) return;

            // Regardless of delete or edit, it is necessary to get the equivalent database information.
            EntityCache.CacheUser ucd = null;
            ulong userId;
            string cacheMsg;
            try
            {
                using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
                {
                    using (var c = db.CreateCommand())
                    {
                        c.CommandText = "SELECT author_id, message FROM " + TableMessage
                            + " WHERE message_id = @MessageId";
                        c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = messageId;
                        c.Prepare();
                        using (var r = await c.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                userId = unchecked((ulong)r.GetInt64(0));
                                cacheMsg = r.GetString(1);
                            }
                            else
                            {
                                userId = 0;
                                cacheMsg = "*(Message not in cache.)*";
                            }
                        }
                    }
                }
                if (userId != 0) ucd = await EntityCache.EntityCache.QueryAsync(guildId, userId);
            }
            catch (NpgsqlException ex)
            {
                await _outLog($"SQL error in {nameof(ProcessReportMessage)}: " + ex.Message);
                cacheMsg = "**Database error. See log.**";
            }

            // Find target channel, prepare and send out message
            var g = _dClient.GetGuild(guildId);
            var rptTargetChannel = g?.GetTextChannel(cfg.RptTarget.Value.Id.Value);
            if (rptTargetChannel == null)
            {
                await _outLog($"WARNING: Reporting channel {cfg.RptTarget.Value.ToString()} could not be determined.");
                return;
            }
            var em = CreateReportEmbed(isDelete, ucd, messageId, ch, (cacheMsg, editMsg));
            await rptTargetChannel.SendMessageAsync("", embed: em);
        }

        const int ReportCutoffLength = 500;
        const string ReportCutoffNotify = "**Message length too long; showing first {0} characters.**\n\n";
        private EmbedBuilder CreateReportEmbed(
            bool isDelete,
            EntityCache.CacheUser ucd, ulong messageId, ISocketMessageChannel chInfo,
            (string, string) content) // Item1 = cached content. Item2 = after-edit message (null if isDelete)
        {
            string msgCached = content.Item1;
            string msgPostEdit = content.Item2;
            if (content.Item1.Length > ReportCutoffLength)
            {
                msgCached = string.Format(ReportCutoffNotify, ReportCutoffLength)
                    + content.Item1.Substring(0, ReportCutoffLength);
            }
            if (!isDelete && content.Item2.Length > ReportCutoffLength)
            {
                msgPostEdit = string.Format(ReportCutoffNotify, ReportCutoffLength)
                    + content.Item2.Substring(0, ReportCutoffLength);
            }

            // Note: Value for ucb can be null if cached user could not be determined.
            var eb = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder()
                {
                    IconUrl = ucd?.AvatarUrl
                },
                Fields = new System.Collections.Generic.List<EmbedFieldBuilder>(),
                Footer = new EmbedFooterBuilder()
                {
                    Text = "User ID: " + ucd?.UserId.ToString() ?? "Unknown",
                    IconUrl = _dClient.CurrentUser.GetAvatarUrl()
                },
                Timestamp = DateTimeOffset.UtcNow
            };

            if (isDelete)
            {
                eb.Author.Name = "Deleted message by ";
                eb.Color = new Color(0xff7373);
                eb.Description = msgCached;
            }
            else
            {
                eb.Author.Name = "Edited message by ";
                eb.Color = new Color(0xffcc40);
                eb.Fields.Add(new EmbedFieldBuilder()
                {
                    Name = "Before",
                    Value = msgCached
                });
                eb.Fields.Add(new EmbedFieldBuilder()
                {
                    Name = "After",
                    Value = msgPostEdit
                });
            }

            eb.Author.Name += ucd == null ? "unknown user" : $"{ucd.Username}#{ucd.Discriminator}";

            var context = new StringBuilder();
            if (ucd != null) context.AppendLine($"Username: <@!{ucd.UserId}>");
            context.AppendLine($"Channel: <#{chInfo.Id}> #{chInfo.Name}");
            context.Append($"Message ID: {messageId}");
            eb.Fields.Add(new EmbedFieldBuilder()
            {
                Name = "Context",
                Value = context.ToString()
            });

            return eb;
        }
        #endregion

        #region Database storage/retrieval
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
                        + "channel_id bigint not null, " // TODO channel cache fk when that gets implemented
                        + "created_ts timestamptz not null, "
                        + "edited_ts timestamptz null, "
                        + "message text not null, "
                        + $"FOREIGN KEY (author_id, guild_id) references {EntityCache.SqlHelper.TableUser} (user_id, guild_id)"
                        + ")";
                    c.ExecuteNonQuery();
                }
            }
        }

        private async Task AddOrUpdateCacheItemAsync(SocketMessage msg)
        {
            try
            {
                using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
                {
                    using (var c = db.CreateCommand())
                    {
                        c.CommandText = "INSERT INTO " + TableMessage
                            + " (message_id, author_id, guild_id, channel_id, created_ts, edited_ts, message) VALUES"
                            + " (@MessageId, @UserId, @GuildId, @ChannelId, @Date, @Edit, @Message)"
                            + " ON CONFLICT (message_id) DO UPDATE"
                            + " SET message = EXCLUDED.message, edited_ts = EXCLUDED.edited_ts";
                        c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = msg.Id;
                        c.Parameters.Add("@UserId", NpgsqlDbType.Bigint).Value = msg.Author.Id;
                        c.Parameters.Add("@GuildId", NpgsqlDbType.Bigint).Value = ((SocketGuildUser)msg.Author).Guild.Id;
                        c.Parameters.Add("@ChannelId", NpgsqlDbType.Bigint).Value = msg.Channel.Id;
                        c.Parameters.Add("@Date", NpgsqlDbType.TimestampTZ).Value = msg.Timestamp;
                        if (msg.EditedTimestamp.HasValue)
                            c.Parameters.Add("@Edit", NpgsqlDbType.TimestampTZ).Value = msg.EditedTimestamp.Value;
                        else
                            c.Parameters.Add("@Edit", NpgsqlDbType.TimestampTZ).Value = DBNull.Value;
                        c.Parameters.Add("@Message", NpgsqlDbType.Text).Value = msg.Content;
                        c.Prepare();
                        await c.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                await _outLog($"SQL error in {nameof(AddOrUpdateCacheItemAsync)}: " + ex.Message);
            }
        }
        
        private async Task<string> GetCachedMessageAsync(ulong messageId)
        {
            try
            {
                using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
                {
                    using (var c = db.CreateCommand())
                    {
                        c.CommandText = "SELECT message FROM " + TableMessage
                            + " WHERE message_id = @MessageId";
                        c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = messageId;
                        c.Prepare();
                        using (var r = await c.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                                return r.GetString(0);
                            else
                                return null;
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                await _outLog($"SQL error in {nameof(GetCachedMessageAsync)}: " + ex.Message);
                return null;
            }
        }
        #endregion
    }
}
