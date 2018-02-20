using Discord;
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
        private async Task Client_MessageReceived(SocketMessage arg) => await AddOrUpdateCacheItemAsync(arg);

        private async Task Client_MessageUpdated(
            Discord.Cacheable<Discord.IMessage, ulong> before,
            SocketMessage after, ISocketMessageChannel channel)
        {
            if (after is SocketUserMessage afterMsg)
            {
                // We're not interested in all message updates, only those that leave a timestamp.
                if (!afterMsg.EditedTimestamp.HasValue) return;
            }
            else return; // no after???

            // Once an edited message is cached, the original message contents are discarded.
            // This is the only time to report it.
            await ProcessReportMessage(false, before.Id, channel, after.Content);
            
            await AddOrUpdateCacheItemAsync(after);
        }

        private async Task Client_MessageDeleted(
            Discord.Cacheable<Discord.IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            await ProcessReportMessage(true, msg.Id, channel, null);
        }
        #endregion

        #region Reporting

        // Reports an edited or deleted message as if it were a log entry (even though it's not).
        private async Task ProcessReportMessage(
            bool isDelete, ulong messageId, ISocketMessageChannel ch, string editMsg)
        {
            var cht = ch as SocketTextChannel;
            if (cht == null)
            {
                // TODO remove debug print
                Console.WriteLine("Incoming message not of a text channel");
                return;
            }
            ulong guildId = cht.Guild.Id;

            // Check if enabled before doing anything else
            var rptTarget = _outGetConfig(guildId) as ConfigItem.EntityName?;
            if (!rptTarget.HasValue) return;

            // Regardless of delete or edit, it is necessary to get database information.
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
            var em = CreateReportEmbed(isDelete, ucd, messageId, ch, (cacheMsg, editMsg));
            var rptTargetChannel = _dClient.GetGuild(guildId)?.GetTextChannel(rptTarget.Value.Id.Value);
            if (rptTargetChannel == null)
            {
                await _outLog("Target channel not found.");
                // TODO make a more descriptive error message
                return;
            }
            await rptTargetChannel.SendMessageAsync("", embed: em);
        }

        const int ReportCutoffLength = 750;
        const string ReportCutoffNotify = "**Message length too long; showing first {0} characters.**\n\n";
        private EmbedBuilder CreateReportEmbed(
            bool isDelete,
            EntityCache.CacheUser ucd, ulong messageId, ISocketMessageChannel chInfo,
            (string, string) content) // tuple: Item1 = cached content. Item2 = after-edit message
        {
            string before = content.Item1;
            string after = content.Item2;
            if (content.Item1.Length > ReportCutoffLength)
            {
                before = string.Format(ReportCutoffNotify, ReportCutoffLength)
                    + content.Item1.Substring(ReportCutoffLength);
            }
            if (isDelete && content.Item2.Length > ReportCutoffLength)
            {
                after = string.Format(ReportCutoffNotify, ReportCutoffLength)
                    + content.Item2.Substring(ReportCutoffLength);
            }

            // Note: Value for ucb is null if cached user could not be determined
            var eb = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder()
                {
                    IconUrl = ucd?.AvatarUrl
                },
                Fields = new System.Collections.Generic.List<EmbedFieldBuilder>(),
                Footer = new EmbedFooterBuilder()
                {
                    Text = (ucd == null ? "" : $"UID {ucd.UserId} - ") + $"MID {messageId}",
                    IconUrl = _dClient.CurrentUser.GetAvatarUrl()
                },
                Timestamp = DateTimeOffset.Now
            };

            if (isDelete)
            {
                eb.Color = new Color(0x9b9b9b);
                eb.Description = content.Item1;
                eb.Author.Name = "Message deleted by "
                    + ucd == null ? "unknown user" : $"{ucd.Username}#{ucd.Discriminator}";
            }
            else
            {
                eb.Color = new Color(8615955);
                eb.Fields.Add(new EmbedFieldBuilder()
                {
                    Name = "Before",
                    Value = before
                });
                eb.Fields.Add(new EmbedFieldBuilder()
                {
                    Name = "After",
                    Value = after
                });
            }
            
            if (ucd != null) eb.Fields.Add(new EmbedFieldBuilder()
            {
                Name = "Username",
                Value = $"<@!{ucd.UserId}>",
                IsInline = true
            });
            eb.Fields.Add(new EmbedFieldBuilder()
            {
                Name = "Channel",
                Value = $"<#{chInfo.Id}>\n#{chInfo.Name}",
                IsInline = true
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
                    // No upsert. Delete, then add.
                    using (var t = db.BeginTransaction())
                    {
                        using (var c = db.CreateCommand())
                        {
                            c.CommandText = "DELETE FROM " + TableMessage + " WHERE message_id = @MessageId";
                            c.Parameters.Add("@MessageId", NpgsqlDbType.Bigint).Value = msg.Id;
                            c.Prepare();
                            await c.ExecuteNonQueryAsync();
                        }
                        using (var c = db.CreateCommand())
                        {
                            c.CommandText = "INSERT INTO " + TableMessage
                                + " (message_id, author_id, guild_id, channel_id, created_ts, edited_ts, message) VALUES "
                                + "(@MessageId, @UserId, @GuildId, @ChannelId, @Date, @Edit, @Message)";
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
