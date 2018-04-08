using Discord;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Represents a log entry in the database.
    /// </summary>
    class LogEntry
    {
        readonly int _logId;
        readonly DateTimeOffset _ts;
        readonly ulong _guildId;
        readonly ulong _invokeId;
        readonly ulong _targetId;
        readonly string _targetString;
        readonly LogType _type;
        readonly string _content;

        // lazy-loaded values:
        private string _invokeName = null;

        /// <summary>
        /// Gets the ID value of this log entry.
        /// </summary>
        public int Id => _logId;
        /// <summary>
        /// Gets the UTC timestamp of the entry.
        /// </summary>
        public DateTimeOffset Timestamp => _ts;
        /// <summary>
        /// Gets the ID of the guild to which this log entry corresponds.
        /// </summary>
        public ulong Guild => _guildId;
        /// <summary>
        /// Gets the ID of the user to which this log entry corresponds.
        /// </summary>
        public ulong TargetId => _targetId;
        /// <summary>
        /// Gets the string containing the target's username and nickname, if applicable.
        /// May be null.
        /// </summary>
        public string TargetString => _targetString;
        /// <summary>
        /// Gets the ID of the user that created this log entry.
        /// This value differs from <see cref="TargetId"/> if this entry was created through
        /// action of another user, such as the issuer of notes and warnings.
        /// </summary>
        public ulong InvokerId => _invokeId;
        /// <summary>
        /// Gets the string containing the invoker's username (not nickname).
        /// This value is not stored in the log entry, but instead retrieved from EntityCache.
        /// </summary>
        public string InvokerName {
            get {
                if (_invokeName == null)
                {
                    var c = EntityCache.EntityCache.QueryUserAsync(_guildId, _invokeId).GetAwaiter().GetResult();
                    _invokeName = $"{c.Username}#{c.Discriminator}";
                }
                return _invokeName;
            }
        }
        /// <summary>
        /// Gets this log entry's type in numeric form.
        /// </summary>
        public LogType Type => _type;
        
        /// <summary>
        /// Gets this log entry's type in string form.
        /// </summary>
        public string TypeName => Enum.GetName(typeof(LogType), _type);
        /// <summary>
        /// Gets the content of this log entry.
        /// </summary>
        public string Content => _content;

        public LogEntry(DbDataReader r)
        {
            // Double-check ordinals if making changes to QueryColumns
            _logId = r.GetInt32(0);
            _ts = r.GetDateTime(1).ToUniversalTime();
            unchecked
            {
                _guildId = (ulong)r.GetInt64(2);
                _targetId = (ulong)r.GetInt64(3);
                if (r.IsDBNull(4)) _targetString = null;
                else _targetString = r.GetString(4);
                _invokeId = (ulong)r.GetInt64(5);
            }
            _type = (LogType)r.GetInt32(6);
            _content = r.GetString(7);
        }

        #region Output
        /// <summary>
        /// Log types in which the invoking user is a moderator.
        /// </summary>
        const LogType TypesWithModeratorInvoker = LogType.Ban | LogType.Warn | LogType.Note;

        /// <summary>
        /// Returns an embed to be used either as a confirmation message or for auto-reporting.
        /// </summary>
        public Embed ToEmbed()
        {
            bool hasIssuer = (_type & TypesWithModeratorInvoker) != 0;
            var result = new EmbedBuilder()
            {
                Footer = new EmbedFooterBuilder() { Text = $"Event {Id}: {TypeName}" },
                Timestamp = this.Timestamp,
                Title = $"{TypeName} {Id} - " + (hasIssuer ? "Issued by " : "From ") + InvokerName,
                Color = new Color(0xffffff), // TODO color differentiation
                Description = Content,
                Fields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder()
                    {
                        Name = "Context"
                    }
                }
            };

            // Context field changes depending on log type
            string target = $"<@{TargetId}>";
            if (TargetString != null) target = $"{TargetString} ({target})";
            if (hasIssuer)
            {
                result.Fields[0].Value = $"**Username:** {target}\n"
                    + $"**Moderator:** {InvokerName} (<@{InvokerId}>)";
            }
            else
            {
                result.Fields[0].Value = $"**Username:** {target}";
            }

            return result;
        }
        /// <summary>
        /// Returns a plain string meant to be used as part of the result of a query.
        /// </summary>
        public string ToQueryResultString()
        {
            // remember to add spaces for indentation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a short string showing the log type and ID.
        /// </summary>
        public override string ToString() => $"{TypeName} {Id}";
        #endregion

        #region Log entry types
        /// <summary>
        /// Enumeration of all possible event flags. Names will show themselves to users
        /// and associated values will be saved to the databaase.
        /// Once included in a release build, they should never be changed again.
        /// </summary>
        [Flags]
        public enum LogType
        {
            /// <summary>Should only be useful in GuildState and ignored elsewhere.</summary>
            None = 0x0,
            Note = 0x1,
            Warn = 0x2,
            Ban = 0x8,
            /// <summary>Record of a user joining a guild.</summary>
            JoinGuild = 0x10,
            /// <summary>Record of a user leaving a guild, voluntarily or by force (kick, ban).</summary>
            LeaveGuild = 0x20,
            NameChange = 0x40,
            /// <summary>Not a database entry, but valid for MessageCache configuration.</summary>
            MsgEdit = 0x8000000,
            /// <summary>Not a database entry, but valid for MessageCache configuration.</summary>
            MsgDelete = 0x10000000
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

        internal static void CreateTable()
        {
            using (var db = RegexBot.Config.GetOpenDatabaseConnectionAsync().GetAwaiter().GetResult())
            {
                using (var c = db.CreateCommand())
                {
                    // Double-check QueryColumns if making changes to this statement
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TblEntry + " ("
                        + "entry_id int primary key, "
                        + "entry_ts timestamptz not null, "
                        + "guild_id bigint not null, "
                        + "target_id bigint not null, " // No foreign constraint: some targets may not be cached
                        + "target_name text null," // some targets may have unknown names
                        + "invoker_id bigint not null, "
                        + "log_type integer not null, "
                        + "contents text not null, "
                        + $"FOREIGN KEY (invoker_id, guild_id) REFERENCES {EntityCache.SqlHelper.TableUser} (user_id, guild_id), "
                        + $"FOREIGN KEY (target_channel_id, guild_id) REFERENCES {EntityCache.SqlHelper.TableTextChannel} (channel_id, guild_id)"
                        + ")";
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

        // Double-check CreateTables and the constructor if making changes to this constant
        const string QueryColumns = "entry_id, entry_ts, guild_id, target_id, target_name, invoker_id, log_type, contents";
        
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

            return result.AsReadOnly();
        }
        #endregion
    }
}
