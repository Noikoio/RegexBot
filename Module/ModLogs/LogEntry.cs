using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Represents a log entry in the database.
    /// </summary>
    class LogEntry
    {
        readonly int _logId;
        readonly DateTime _ts;
        readonly ulong _guildId;
        readonly ulong? _invokeId;
        readonly ulong _targetId;
        readonly ulong? _channelId;
        readonly LogType _type;
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
        /// Gets this log entry's type.
        /// </summary>
        public LogType Type => _type;
        /// <summary>
        /// Gets the content of this log entry.
        /// </summary>
        public string Message => _message;

        public LogEntry(DbDataReader r)
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
            _type = (LogType)r.GetInt32(6);
            _message = r.GetString(7);
        }

        // TODO lazy loading of channel, user, etc from caches
        // TODO methods for updating this log entry(?)

        // TODO figure out some helper methods to retrieve data of other entities by ID, if it becomes necessary

        #region Log entry types
        /// <summary>
        /// Enumeration of all possible event flags. Names will show themselves to users
        /// and associated values will be saved to the databaase.
        /// Once they're included in a release build, they should never be changed again.
        /// </summary>
        [Flags]
        public enum LogType
        {
            /// <summary>Should only be useful in GuildState and ignored elsewhere.</summary>
            None = 0x0,
            Note = 0x1,
            Warn = 0x2,
            Kick = 0x4,
            Ban = 0x8,
            /// <summary>Record of a user joining a guild.</summary>
            JoinGuild = 0x10,
            /// <summary>Record of a user leaving a guild, voluntarily or by force (kick, ban).</summary>
            LeaveGuild = 0x20,
            NameChange = 0x40,
            /// <summary>Not a database entry, but exists for MessageCache configuration.</summary>
            MsgEdit = 0x80,
            /// <summary>Not a database entry, but exists for MessageCache configuration.</summary>
            MsgDelete = 0x100
        }

        public static LogType GetLogTypeFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Types are not properly defined.");

            var strTypes = input.Split(
                new char[] { ' ', ',', '/', '+' }, // and more?
                StringSplitOptions.RemoveEmptyEntries);

            LogType endResult = LogType.None;
            foreach (var item in strTypes)
            {
                try
                {
                    var result = Enum.Parse<LogType>(item, true);
                    endResult |= result;
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException($"Unable to determine the given event type \"{item}\".");
                }
            }

            return endResult;
        }
        #endregion

        #region SQL setup and querying
        public const string TblEntry = "modlogs_entries";
        public const string TblEntryIncr = TblEntry + "_id";

        internal static void CreateTables()
        {
            using (var db = RegexBot.Config.GetOpenDatabaseConnectionAsync().GetAwaiter().GetResult())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TblEntry + " ("
                        + "id int primary key, "
                        + "entry_ts timestamptz not null, "
                        + "guild_id bigint not null, "
                        + "target_id bigint not null, "
                        + $"invoke_id bigint null references {EntityCache.SqlHelper.TableUser}.user_id, "
                        + "target_channel_id bigint null, " // TODO channel cache reference?
                        + "entry_type integer not null, "
                        + "message text not null, "
                        + $"FOREIGN KEY (target_id, guild_id) REFERENCES {EntityCache.SqlHelper.TableUser} (user_id, guild_id)";
                    c.ExecuteNonQuery();
                }
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {TblEntryIncr} "
                        + $"START 1000 MAXVALUE {int.MaxValue}";
                    c.ExecuteNonQuery();
                }
            }
        }

        // Double-check constructor if making changes to this constant
        const string QueryColumns = "id, entry_ts, guild_id, target_id, invoke_id, target_channel_id, entry_type, message";
        
        /// <summary>
        /// Attempts to look up a log entry by its ID.
        /// </summary>
        /// <returns>Null if no result.</returns>
        public static async Task<LogEntry> QueryIdAsync(ulong guild, int id)
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {TblEntry} "
                        + "WHERE guild_id = @Guild and id = @Id";
                    c.Parameters.Add("@Guild", NpgsqlTypes.NpgsqlDbType.Bigint).Value = guild;
                    c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Numeric).Value = id;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (r.Read()) return new LogEntry(r);
                        else return null;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to look up a log entry by a number of parameters.
        /// At least "target" or "invoker" are required when calling this method.
        /// </summary>
        /// <returns></returns>
        public static async Task<IEnumerable<LogEntry>> QueryLogAsync
            (ulong guild,
            ulong? target = null,
            ulong? invoker = null,
            ulong? channel = null,
            LogType? category = null)
        {
            // Enforce some limits - can't search too broadly here. Requires this at a minimum:
            if (target.HasValue == false && invoker.HasValue == false)
            {
                throw new ArgumentNullException("Query requires at minimum searching of a target or invoker.");
            }

            var result = new List<LogEntry>();
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {TblEntry} WHERE";

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
                    if (category.HasValue)
                    {
                        if (and) c.CommandText += " AND";
                        else and = true;
                        c.CommandText += " entry_type = @Category";
                        c.Parameters.Add("@Category", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)category;
                    }
                    c.Prepare();

                    using (var r = await c.ExecuteReaderAsync())
                    {
                        while (r.Read())
                        {
                            result.Add(new LogEntry(r));
                        }
                    }
                }
            }

            return result;
        }
        #endregion
    }
}
