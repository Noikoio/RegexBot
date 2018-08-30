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
        private readonly Func<ulong, GuildState> _outGetConfig;

        // TODO: How to clear the cache after a time? Can't hold on to this forever.
        // TODO Do not store messages at all if features is disabled.

        public MessageCache(DiscordSocketClient client, AsyncLogger logger, Func<ulong, GuildState> getConfFunc)
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
            if (arg.Author.IsWebhook) return;
            if (arg.Channel is IDMChannel) return; // No DMs

            await AddOrUpdateCacheItemAsync(arg);
        }

        private async Task Client_MessageUpdated(
            Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsWebhook) return;

            // We only want channel messages
            if (after is SocketUserMessage afterMsg && !(afterMsg is IDMChannel))
            {
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
            var cfg = _outGetConfig(guildId);
            if (cfg == null) return;
            if (cfg.RptIgnore != 0 && ch.Id == cfg.RptIgnore) return; // ignored channel
            if (isDelete && (cfg.RptTypes & LogEntry.LogType.MsgDelete) == 0) return; // not reporting deletions
            if (!isDelete && (cfg.RptTypes & LogEntry.LogType.MsgEdit) == 0) return; // not reporting edits

            // Regardless of delete or edit, it is necessary to get the equivalent database information.
            EntityCache.CacheUser ucd = null;
            ulong msgAuthorId;
            string msgContent;
            DateTimeOffset msgCreateTime;
            DateTimeOffset? msgEditTime = null;
            try
            {
                using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
                {
                    using (var c = db.CreateCommand())
                    {
                        c.CommandText = "SELECT author_id, message, created_ts, edited_ts as msgtime FROM " + TableMessage
                            + " WHERE message_id = @MessageId";
                        c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = messageId;
                        c.Prepare();
                        using (var r = await c.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                msgAuthorId = unchecked((ulong)r.GetInt64(0));
                                msgContent = r.GetString(1);
                                msgCreateTime = r.GetDateTime(2).ToUniversalTime();
                                if (r.IsDBNull(3)) msgEditTime = null;
                                else msgEditTime = r.GetDateTime(3).ToUniversalTime();
                            }
                            else
                            {
                                msgAuthorId = 0;
                                msgContent = "*(Message not in cache.)*";
                                msgCreateTime = DateTimeOffset.UtcNow;
                                msgEditTime = null;
                            }
                        }
                    }
                }
                if (msgAuthorId != 0) ucd = await EntityCache.EntityCache.QueryUserAsync(guildId, msgAuthorId);
            }
            catch (NpgsqlException ex)
            {
                await _outLog($"SQL error in {nameof(ProcessReportMessage)}: " + ex.Message);
                msgContent = "**Database error. See log.**";
                msgCreateTime = DateTimeOffset.UtcNow;
            }

            // Prepare and send out message
            var em = CreateReportEmbed(isDelete, ucd, messageId, ch, (msgContent, editMsg), msgCreateTime, msgEditTime);
            await cfg.RptTarget.SendMessageAsync("", embeds: new Embed[] { em });
        }

        const int ReportCutoffLength = 500;
        const string ReportCutoffNotify = "**Message length too long; showing first {0} characters.**\n\n";
        private EmbedBuilder CreateReportEmbed(
            bool isDelete,
            EntityCache.CacheUser ucd, ulong messageId, ISocketMessageChannel chInfo,
            (string, string) content, // Item1 = cached content. Item2 = post-edit message (null if isDelete)
            DateTimeOffset msgCreated, DateTimeOffset? msgEdited)
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
            if (string.IsNullOrEmpty(msgCached)) msgCached = "[blank message]";
            if (string.IsNullOrEmpty(msgPostEdit)) msgPostEdit = "[blank message]";

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
                    Text = "User ID: " + ucd?.UserId.ToString() ?? "Unknown"
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
            if (msgEdited.HasValue)
                context.AppendLine($"Prior edit date: {MakeTimestampString(msgEdited.Value)}");
            else
                context.AppendLine($"Post date: {MakeTimestampString(msgCreated)}");
            context.Append($"Message ID: {messageId}");

            eb.Fields.Add(new EmbedFieldBuilder()
            {
                Name = "Context",
                Value = context.ToString()
            });

            return eb;
        }

        private string MakeTimestampString(DateTimeOffset time)
        {
            var result = new StringBuilder();
            result.Append(time.ToString("yyyy-MM-dd hh:mm:ss"));

            var now = DateTimeOffset.UtcNow;
            var diff = now - time;
            if (diff < new TimeSpan(3, 0, 0, 0))
            {
                // Difference less than 3 days. Generate relative time format.
                result.Append(" - ");

                if (diff.TotalSeconds < 60)
                {
                    // Under a minute ago. Show only seconds.
                    result.Append((int)Math.Ceiling(diff.TotalSeconds) + "s ago");
                }
                else
                {
                    // over a minute. Show days, hours, minutes, seconds.
                    var ts = (int)Math.Ceiling(diff.TotalSeconds);
                    var m = (ts % 3600) / 60;
                    var h = (ts % 86400) / 3600;
                    var d = ts / 86400;

                    if (d > 0) result.AppendFormat("{0}d{1}h{2}m", d, h, m);
                    else if (h > 0) result.AppendFormat("{0}h{1}m", h, m);
                    else result.AppendFormat("{0}m", m);
                    result.Append(" ago");
                }
            }

            return result.ToString();
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
            // Insert attachment file names into cache
            // Doing this only here causes this information to appear only in database results.
            // That is, message deletions and pre-edits.
            var dbinsert = new StringBuilder();
            if (msg.Attachments.Count > 0)
            {
                dbinsert.Append("[Attached: ");
                foreach (var item in msg.Attachments)
                {
                    dbinsert.Append(item.Filename);
                    dbinsert.Append(", ");
                }
                dbinsert.Length -= 2;
                dbinsert.AppendLine("]");
            }
            dbinsert.Append(msg.Content);

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
                        c.Parameters.Add("@Message", NpgsqlDbType.Text).Value = dbinsert.ToString();
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
