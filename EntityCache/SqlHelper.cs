using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    /// <summary>
    /// Helper methods for database operations.
    /// Exceptions are not handled within methods of this class.
    /// </summary>
    static class SqlHelper
    {
        public const string TableGuild = "cache_guild";
        public const string TableTextChannel = "cache_textchannel";
        public const string TableUser = "cache_users";

        private static async Task<NpgsqlConnection> OpenDB()
        {
            if (!RegexBot.Config.Database.Available) return null;
            return await RegexBot.Config.Database.GetOpenConnectionAsync();
        }

        internal static async Task CreateCacheTablesAsync()
        {
            var db = await OpenDB();
            if (db == null) return;
            using (db)
            {
                // Guild cache
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableGuild + " ("
                        + "guild_id bigint primary key, "
                        + "cache_date timestamptz not null, "
                        + "current_name text not null, "
                        + "display_name text null"
                        + ")";
                    await c.ExecuteNonQueryAsync();
                }
                // May not require other indexes. Add here if they become necessary.

                // Text channel cache
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableTextChannel + " ("
                        + "channel_id bigint not null primary key, "
                        + $"guild_id bigint not null references {TableGuild}, "
                        + "cache_date timestamptz not null, "
                        + "name text not null";
                    await c.ExecuteNonQueryAsync();
                }
                // As of the time of this commit, Discord doesn't allow any uppercase characters
                // in channel names. No lowercase name index needed.

                // User cache
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableUser + " ("
                        + "user_id bigint not null, "
                        + $"guild_id bigint not null references {TableGuild}, "
                        + "cache_date timestamptz not null, "
                        + "username text not null, "
                        + "discriminator text not null, "
                        + "nickname text null, "
                        + "avatar_url text null"
                        + ")";
                    await c.ExecuteNonQueryAsync();
                }
                using (var c = db.CreateCommand())
                {
                    // guild_id is a foreign key, and also one half of the primary key here
                    c.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS "
                        + $"{TableUser}_ck_idx on {TableUser} (user_id, guild_id)";
                    await c.ExecuteNonQueryAsync();
                }
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE INDEX IF NOT EXISTS "
                        + $"{TableUser}_usersearch_idx on {TableUser} LOWER(username)";
                    await c.ExecuteNonQueryAsync();
                }
            }
        }

        #region Insertions and updates
        internal static async Task UpdateGuildAsync(SocketGuild g)
        {
            var db = await OpenDB();
            if (db == null) return;
            using (db)
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "INSERT INTO " + Sql.TableGuild + " (guild_id, current_name) "
                        + "VALUES (@GuildId, @CurrentName) "
                        + "ON CONFLICT (guild_id) DO UPDATE SET "
                        + "current_name = EXCLUDED.current_name";
                    c.Parameters.Add("@GuildId", NpgsqlDbType.Bigint).Value = g.Id;
                    c.Parameters.Add("@CurrentName", NpgsqlDbType.Text).Value = g.Name;
                    c.Prepare();
                    await c.ExecuteNonQueryAsync();
                }
            }
        }

        internal static Task UpdateGuildMemberAsync(SocketGuildUser user)
        {
            var ml = new SocketGuildUser[] { user };
            return UpdateGuildMemberAsync(ml);
        }
        internal static async Task UpdateGuildMemberAsync(IEnumerable<SocketGuildUser> users)
        {
            var db = await OpenDB();
            if (db == null) return;
            using (db)
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "INSERT INTO " + Sql.TableUser
                        + " (user_id, guild_id, cache_date, username, discriminator, nickname, avatar_url)"
                        + " VALUES (@Uid, @Gid, @Date, @Uname, @Disc, @Nname, @Url) "
                        + "ON CONFLICT (user_id, guild_id) DO UPDATE SET "
                        + "cache_date = EXCLUDED.cache_date, username = EXCLUDED.username, "
                        + "discriminator = EXCLUDED.discriminator, " // I've seen someone's discriminator change this one time...
                        + "nickname = EXCLUDED.nickname, avatar_url = EXCLUDED.avatar_url";

                    var uid = c.Parameters.Add("@Uid", NpgsqlDbType.Bigint);
                    var gid = c.Parameters.Add("@Gid", NpgsqlDbType.Bigint);
                    c.Parameters.Add("@Date", NpgsqlDbType.TimestampTZ).Value = DateTime.Now;
                    var uname = c.Parameters.Add("@Uname", NpgsqlDbType.Text);
                    var disc = c.Parameters.Add("@Disc", NpgsqlDbType.Text);
                    var nname = c.Parameters.Add("@Nname", NpgsqlDbType.Text);
                    var url = c.Parameters.Add("@Url", NpgsqlDbType.Text);
                    c.Prepare();

                    foreach (var item in users)
                    {
                        if (item.IsBot || item.IsWebhook) continue;

                        uid.Value = item.Id;
                        gid.Value = item.Guild.Id;
                        uname.Value = item.Username;
                        disc.Value = item.Discriminator;
                        nname.Value = item.Nickname;
                        if (nname.Value == null) nname.Value = DBNull.Value; // why can't ?? work here?
                        url.Value = item.GetAvatarUrl();
                        if (url.Value == null) url.Value = DBNull.Value;

                        await c.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        #endregion
    }
}
