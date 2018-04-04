using Discord.WebSocket;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    class CacheChannel
    {
        readonly ulong _channelId;
        readonly ulong _guildId;
        readonly DateTimeOffset _cacheDate;
        readonly string _channelName;

        private CacheChannel(SocketGuildChannel c)
        {
            _channelId = c.Id;
            _guildId = c.Guild.Id;
            _cacheDate = DateTimeOffset.UtcNow;
            _channelName = c.Name;
        }

        // Double-check SqlHelper if making changes to this constant
        const string QueryColumns = "channel_id, guild_id, cache_date, channel_name";
        private CacheChannel(DbDataReader r)
        {
            // Double-check ordinals if making changes to QueryColumns
            unchecked
            {
                _channelId = (ulong)r.GetInt64(0);
                _guildId = (ulong)r.GetInt64(1);
            }
            _cacheDate = r.GetDateTime(2).ToUniversalTime();
            _channelName = r.GetString(3);
        }

        #region Queries
        // Accessible by EntityCache. Documentation is there.
        internal static async Task<CacheChannel> QueryAsync(DiscordSocketClient c, ulong guild, ulong channel)
        {
            // Local cache search
            var lresult = LocalQueryAsync(c, guild, channel);
            if (lresult != null) return lresult;

            // Database cache search
            var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync();
            if (db == null) return null; // Database not available for query.
            return await DbQueryAsync(db, guild, channel);
        }

        private static CacheChannel LocalQueryAsync(DiscordSocketClient c, ulong guild, ulong channel)
        {
            var ch = c.GetGuild(guild)?.GetChannel(channel);
            if (ch == null) return null;
            return new CacheChannel(ch);
        }
        private static async Task<CacheChannel> DbQueryAsync(NpgsqlConnection db, ulong guild, ulong channel)
        {
            using (db)
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} from {SqlHelper.TableTextChannel} WHERE "
                        + "channel_id = @Cid AND guild_id = @Gid";
                    c.Parameters.Add("@Cid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = channel;
                    c.Parameters.Add("@Gid", NpgsqlTypes.NpgsqlDbType.Bigint).Value = guild;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            return new CacheChannel(r);
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

        // Accessible by EntityCache. Documentation is there.
        internal static async Task<IEnumerable<CacheChannel>> QueryAsync(DiscordSocketClient c, ulong guild, string search)
        {
            // Is search just a number? Assume ID, pass it on to the correct place.
            if (ulong.TryParse(search, out var presult))
            {
                var r = await QueryAsync(c, guild, presult);
                if (r == null) return new CacheChannel[0];
                else return new CacheChannel[] { r };
            }

            // Split leading # from name, if exists
            if (search.Length > 0 && search[0] == '#') search = search.Substring(1);

            // Local cache search
            var lresult = LocalQueryAsync(c, guild, search);
            if (lresult.Count() != 0) return lresult;

            // Database cache search
            var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync();
            if (db == null) return new CacheChannel[0];
            return await DbQueryAsync(db, guild, search);
        }

        private static IEnumerable<CacheChannel> LocalQueryAsync(DiscordSocketClient c, ulong guild, string search)
        {
            var g = c.GetGuild(guild);
            if (g == null) return new CacheChannel[0];

            var qresult = g.Channels
                .Where(i => string.Equals(i.Name, search, StringComparison.InvariantCultureIgnoreCase));
            var result = new List<CacheChannel>();
            foreach (var item in qresult)
            {
                result.Add(new CacheChannel(item));
            }
            return result;
        }

        private static async Task<IEnumerable<CacheChannel>> DbQueryAsync(NpgsqlConnection db, ulong guild, string search)
        {
            var result = new List<CacheChannel>();

            using (db)
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"SELECT {QueryColumns} FROM {SqlHelper.TableTextChannel} WHERE"
                        + " name = lower(@NameSearch)" // all channel names presumed to be lowercase already
                        + " ORDER BY cache_date desc, name";
                    c.Parameters.Add("@NameSearch", NpgsqlTypes.NpgsqlDbType.Text).Value = search;
                    c.Prepare();

                    using (var r = await c.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            result.Add(new CacheChannel(r));
                        }
                    }
                }
            }
            return result;
        }
        #endregion
    }
}
