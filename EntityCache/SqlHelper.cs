using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    /// <summary>
    /// Helper methods for database operations.
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

        public static async Task CreateCacheTables()
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
        static async Task UpdateGuild()
        {
            var db = await OpenDB();
            if (db == null) return;
            using (db)
            {

            }
        }
        #endregion
    }
}
