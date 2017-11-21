using System;
using System.Collections.Generic;
using System.Text;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Contains common constants and static methods used for accessing the log database.
    /// </summary>
    class Sql
    {
        public const string TableLog = "modlogs_entries";
        public const string TableLogIncr = TableLog + "_id";
        public const string TableMsgCache = "modlogs_msgcache";

        static void CreateTables()
        {
            using (var db = RegexBot.Config.Database.GetOpenConnectionAsync().GetAwaiter().GetResult())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableLog + " ("
                        + "id int primary key, "
                        + "entry_ts timestamptz not null, "
                        + "guild_id bigint not null, "
                        + "target_id bigint not null, "
                        + $"invoke_id bigint null references {EntityCache.Sql.TableUser}.user_id, "
                        + "target_channel_id bigint null, " // TODO channel cache reference?
                        + "category text not null, "
                        + "message text not null, "
                        + $"FOREIGN KEY (target_id, guild_id) REFERENCES {EntityCache.Sql.TableUser} (user_id, guild_id)";
                    c.ExecuteNonQuery();
                }
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {TableLogIncr} "
                        + $"START 100 MAXVALUE {int.MaxValue}";
                    c.ExecuteNonQuery();
                }
            }
        }

        #region Log entry manipulation
        // what was I doing again
        #endregion
    }
}
