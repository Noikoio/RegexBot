using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.EntityCache
{
    /// <summary>
    /// Represents a cached user.
    /// </summary>
    class UserCacheItem
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


        private UserCacheItem(DbDataReader r)
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
        const string QueryColumns = "user_id, guild_id, cache_date, username, discriminator, nickname, avatar_url";

        /// <summary>
        /// Attempts to query for an exact result with the given parameters.
        /// </summary>
        /// <returns>Null on no result.</returns>
        public static async Task<UserCacheItem> QueryAsync(ulong guild, ulong user)
        {
            using (var db = await RegexBot.Config.Database.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {Sql.TableUser} WHERE "
                        + "user_id = @Uid AND guild_id = @Gid";
                    c.Parameters.Add("@Uid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = user;
                    c.Parameters.Add("@Gid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = guild;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            return new UserCacheItem(r);
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        private static Regex DiscriminatorSearch = new Regex(@"(.+)#(\d{4}(?!\d))", RegexOptions.Compiled);

        /// <summary>
        /// Attempts to look up the user given a search string.
        /// This string looks up case-insensitive, exact matches of nicknames and usernames.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> containing zero or more query results, sorted by cache date.</returns>
        public static async Task<IEnumerable<UserCacheItem>> QueryAsync(ulong guild, string search)
        {
            // Is search just a number? It's an ID.
            if (ulong.TryParse(search, out var presult))
            {
                var r = await QueryAsync(guild, presult);
                if (r == null) return new UserCacheItem[0];
                else return new UserCacheItem[] { r };
            }

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

            // Storing in HashSet to enforce uniqueness
            HashSet<UserCacheItem> result = new HashSet<UserCacheItem>(_uc);

            using (var db = await RegexBot.Config.Database.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {Sql.TableUser} WHERE "
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
                            result.Add(new UserCacheItem(r));
                        }
                    }
                }
            }
            return result;
        }

        private static UniqueChecker _uc = new UniqueChecker();
        class UniqueChecker : IEqualityComparer<UserCacheItem>
        {
            public bool Equals(UserCacheItem x, UserCacheItem y) => x.UserId == y.UserId && x.GuildId == y.GuildId;

            public int GetHashCode(UserCacheItem obj) => unchecked((int)(obj.UserId ^ obj.GuildId));
        }
        #endregion
    }
}
