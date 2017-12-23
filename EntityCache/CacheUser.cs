using Discord.WebSocket;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    /// <summary>
    /// Representation of a cached user.
    /// </summary>
    class CacheUser
    {
        readonly ulong _userId;
        readonly ulong _guildId;
        readonly DateTime _cacheDate;
        readonly string _username;
        readonly string _discriminator;
        readonly string _nickname;
        readonly string _avatarUrl;

        /// <summary>
        /// The cached user's ID (snowflake) value.
        /// </summary>
        public ulong UserId => _userId;
        /// <summary>
        /// The guild ID (snowflake) for which this user information corresponds to.
        /// </summary>
        public ulong GuildId => _guildId;
        /// <summary>
        /// Timestamp value for when this cache item was last updated, in universal time.
        /// </summary>
        public DateTime CacheDate => _cacheDate;

        /// <summary>
        /// Display name, including discriminator. Shows the nickname, if available.
        /// </summary>
        public string DisplayName => (_nickname ?? _username) + "#" + _discriminator;
        /// <summary>
        /// String useful for tagging the user.
        /// </summary>
        public string Mention => $"<@{_userId}>";
        /// <summary>
        /// User's cached nickname in the guild. May be null.
        /// </summary>
        public string Nickname => _nickname;
        /// <summary>
        /// User's cached username.
        /// </summary>
        public string Username => _username;
        /// <summary>
        /// User's cached discriminator value.
        /// </summary>
        public string Discriminator => _discriminator;
        /// <summary>
        /// URL for user's last known avatar. May be null or invalid.
        /// </summary>
        public string AvatarUrl => _avatarUrl;

        private CacheUser(SocketGuildUser u)
        {
            _userId = u.Id;
            _guildId = u.Guild.Id;
            _cacheDate = DateTime.UtcNow;
            _username = u.Username;
            _discriminator = u.Discriminator;
            _nickname = u.Nickname;
            _avatarUrl = u.GetAvatarUrl();
        }

        // Double-check SqlHelper if making changes to this constant
        const string QueryColumns = "user_id, guild_id, cache_date, username, discriminator, nickname, avatar_url";
        private CacheUser(DbDataReader r)
        {
            // Double-check ordinals if making changes to QueryColumns
            unchecked
            {
                // PostgreSQL does not support unsigned 64-bit numbers. Must convert.
                _userId = (ulong)r.GetInt64(0);
                _guildId = (ulong)r.GetInt64(1);
            }
            _cacheDate = r.GetDateTime(2).ToUniversalTime();
            _username = r.GetString(3);
            _discriminator = r.GetString(4);
            _nickname = r.IsDBNull(5) ? null : r.GetString(5);
            _avatarUrl = r.IsDBNull(6) ? null : r.GetString(6);
        }

        public override string ToString() => DisplayName;

        #region Queries
        // Accessible by EntityCache. Documentation is there.
        internal static async Task<CacheUser> QueryAsync(DiscordSocketClient c, ulong guild, ulong user)
        {
            // Local cache search
            var lresult = LocalQueryAsync(c, guild, user);
            if (lresult != null) return lresult;

            // Database cache search
            var db = await RegexBot.Config.Database.GetOpenConnectionAsync();
            if (db == null) return null; // Database not available for query.
            using (db) return await DbQueryAsync(db, guild, user);
        }

        private static CacheUser LocalQueryAsync(DiscordSocketClient c, ulong guild, ulong user)
        {
            var u = c.GetGuild(guild)?.GetUser(user);
            if (u == null) return null;
            return new CacheUser(u);
        }
        private static async Task<CacheUser> DbQueryAsync(NpgsqlConnection db, ulong guild, ulong user)
        {
            using (db)
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {SqlHelper.TableUser} WHERE "
                        + "user_id = @Uid AND guild_id = @Gid";
                    c.Parameters.Add("@Uid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = user;
                    c.Parameters.Add("@Gid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = guild;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            return new CacheUser(r);
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        // -----

        private static Regex DiscriminatorSearch = new Regex(@"(.+)#(\d{4}(?!\d))", RegexOptions.Compiled);
        // Accessible by EntityCache. Documentation is there.
        internal static async Task<IEnumerable<CacheUser>> QueryAsync(DiscordSocketClient c, ulong guild, string search)
        {
            // Is search just a number? Assume ID, pass it on to the correct place.
            if (ulong.TryParse(search, out var presult))
            {
                var r = await QueryAsync(c, guild, presult);
                if (r == null) return new CacheUser[0];
                else return new CacheUser[] { r };
            }

            // Split name/discriminator
            string name;
            string disc;
            var split = DiscriminatorSearch.Match(search);
            if (split.Success)
            {
                name = split.Groups[1].Value;
                disc = split.Groups[2].Value;
            }
            else
            {
                name = search;
                disc = null;
            }

            // Local cache search
            var lresult = LocalQueryAsync(c, guild, name, disc);
            if (lresult.Count() != 0) return lresult;

            // Database cache search
            var db = await RegexBot.Config.Database.GetOpenConnectionAsync();
            if (db == null) return null; // Database not available for query.
            using (db) return await DbQueryAsync(db, guild, name, disc);
        }

        private static IEnumerable<CacheUser> LocalQueryAsync(DiscordSocketClient c, ulong guild, string name, string disc)
        {
            var g = c.GetGuild(guild);
            if (g == null) return new CacheUser[] { };

            bool Filter(string iun, string inn, string idc)
            {
                // Same logic as in the SQL query in the method below this one
                bool match =
                    string.Equals(iun, name, StringComparison.InvariantCultureIgnoreCase)
                    || string.Equals(inn, name, StringComparison.InvariantCultureIgnoreCase);

                if (match && disc != null)
                    match = idc.Equals(disc);

                return match;
            }

            var qresult = g.Users.Where(i => Filter(i.Username, i.Nickname, i.Discriminator));
            var result = new List<CacheUser>();
            foreach (var item in qresult)
            {
                result.Add(new CacheUser(item));
            }
            return result;
        }

        private static async Task<IEnumerable<CacheUser>> DbQueryAsync(NpgsqlConnection db, ulong guild, string name, string disc)
        {
            var result = new List<CacheUser>();

            using (db = await RegexBot.Config.Database.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {SqlHelper.TableUser} WHERE "
                        + "( lower(username) = lower(@NameSearch) OR lower(nickname) = lower(@NameSearch) )";
                    c.Parameters.Add("@NameSearch", NpgsqlTypes.NpgsqlDbType.Text).Value = name;
                    if (disc != null)
                    {
                        c.CommandText += " AND discriminator = @DiscSearch";
                        c.Parameters.Add("@DiscSearch", NpgsqlTypes.NpgsqlDbType.Text).Value = disc;
                    }
                    c.Prepare();

                    using (var r = await c.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            result.Add(new CacheUser(r));
                        }
                    }
                }
            }
            return result;
        }
        #endregion
    }
}
