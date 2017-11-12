using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;

namespace Noikoio.RegexBot.Module.EntityCache
{
    /// <summary>
    /// Contains common constants and static methods for cache access.
    /// </summary>
    class Sql
    {
        public const string TableGuild = "cache_guild";
        public const string TableUser = "cache_users";

        private static NpgsqlConnection OpenDB() =>
            RegexBot.Config.Database.GetOpenConnectionAsync().GetAwaiter().GetResult();

        public static void CreateCacheTables()
        {
            using (var db = OpenDB())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableGuild + "("
                        + "guild_id bigint primary key, "
                        + "current_name text not null, "
                        + "display_name text null"
                        + ")";
                    c.ExecuteNonQuery();
                }

                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableUser + "("
                        + "user_id bigint not null, "
                        + $"guild_id bigint not null references {TableGuild}, "
                        + "cache_date timestamptz not null, "
                        + "username text not null, "
                        + "discriminator text not null, "
                        + "nickname text null, "
                        + "avatar_url text null"
                        + ")";
                    c.ExecuteNonQuery();
                }
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS "
                        + $"{TableUser}_idx on {TableUser} (user_id, guild_id)";
                    c.ExecuteNonQuery();
                }
                // TODO create indexes for string-based queries
            }
        }
    }
}
