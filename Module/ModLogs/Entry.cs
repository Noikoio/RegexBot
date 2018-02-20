using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Represents a log entry in the database.
    /// </summary>
    class Entry
    {
        readonly int _logId;
        readonly DateTime _ts;
        readonly ulong _guildId;
        readonly ulong? _invokeId;
        readonly ulong _targetId;
        readonly ulong? _channelId;
        readonly string _type;
        readonly string _message;

        /// <summary>
        /// Gets the ID value of this log entry.
        /// </summary>
        public int Id => _logId;
        /// <summary>
        /// Gets the timestamp (a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>) of the entry.
        /// </summary>
        public DateTime Timestamp => _ts;
        /// <summary>
        /// Gets the ID of the guild to which this log entry corresponds.
        /// </summary>
        public ulong Guild => _guildId;
        /// <summary>
        /// Gets the ID of the user to which this log entry corresponds.
        /// </summary>
        public ulong Target => _targetId;
        /// <summary>
        /// Gets the ID of the invoking user.
        /// This value exists only if this entry was created through action of another user that is not the target.
        /// </summary>
        public ulong? Invoker => _invokeId;
        /// <summary>
        /// Gets the guild channel ID to which this log entry corresponds, if any.
        /// </summary>
        public ulong? TargetChannel => _channelId;
        /// <summary>
        /// Gets this log entry's category.
        /// </summary>
        public string Category => _type;
        /// <summary>
        /// Gets the content of this log entry.
        /// </summary>
        public string Message => _message;

        public Entry(DbDataReader r)
        {
            // Double-check ordinals if making changes to QueryColumns

            _logId = r.GetInt32(0);
            _ts = r.GetDateTime(1).ToUniversalTime();
            unchecked
            {
                _guildId = (ulong)r.GetInt64(2);
                _targetId = (ulong)r.GetInt64(3);
                if (r.IsDBNull(4)) _invokeId = null;
                else _invokeId = (ulong)r.GetInt64(4);
                if (r.IsDBNull(5)) _channelId = null;
                else _channelId = (ulong)r.GetInt64(5);
            }
            _type = r.GetString(6);
            _message = r.GetString(7);
        }

        // TODO lazy loading of channel, user, etc from caches
        // TODO methods for updating this log entry(?)

        // TODO figure out some helper methods to retrieve data of other entities by ID, if it becomes necessary

        #region Queries
        // Double-check constructor if making changes to this constant
        const string QueryColumns = "id, entry_ts, guild_id, target_id, invoke_id, target_channel_id, category, message";
        
        /// <summary>
        /// Attempts to look up a log entry with the given ID.
        /// </summary>
        /// <returns>Null if no result.</returns>
        public static async Task<Entry> QueryIdAsync(ulong guild, int id)
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {Sql.TableLog} "
                        + "WHERE guild_id = @Guild and id = @Id";
                    c.Parameters.Add("@Guild", NpgsqlTypes.NpgsqlDbType.Bigint).Value = guild;
                    c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Numeric).Value = id;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (r.Read()) return new Entry(r);
                        else return null;
                    }
                }
            }
        }

        public static async Task<IEnumerable<Entry>> QueryLogAsync
            (ulong guild,
            ulong? target = null,
            ulong? invoker = null,
            ulong? channel = null,
            IEnumerable<string> category = null)
        {
            // Enforce some limits - can't search too broadly here. Requires this at a minimum:
            if (target.HasValue == false && invoker.HasValue == false)
            {
                throw new ArgumentNullException("Query requires at minimum searching of a target or invoker.");
            }

            var result = new List<Entry>();
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {Sql.TableLog} WHERE";

                    bool and = false;
                    if (target.HasValue)
                    {
                        if (and) c.CommandText += " AND";
                        else and = true;
                        c.CommandText += " target_id = @TargetId";
                        c.Parameters.Add("@TargetId", NpgsqlTypes.NpgsqlDbType.Bigint).Value = target.Value;
                    }
                    if (invoker.HasValue)
                    {
                        if (and) c.CommandText += " AND";
                        else and = true;
                        c.CommandText += " invoke_id = @InvokeId";
                        c.Parameters.Add("@InvokeId", NpgsqlTypes.NpgsqlDbType.Bigint).Value = invoker.Value;
                    }
                    if (channel.HasValue)
                    {
                        if (and) c.CommandText += " AND";
                        else and = true;
                        c.CommandText += " target_channel_id = @ChannelId";
                        c.Parameters.Add("@ChannelId", NpgsqlTypes.NpgsqlDbType.Bigint).Value = channel.Value;
                    }
                    c.Prepare();

                    using (var r = await c.ExecuteReaderAsync())
                    {
                        while (r.Read())
                        {
                            result.Add(new Entry(r));
                        }
                    }
                }
            }

            return result;
        }
        #endregion
    }
}
