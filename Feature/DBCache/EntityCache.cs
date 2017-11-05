using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.DBCache
{
    /// <summary>
    /// Caches information regarding all known guilds, channels, and users.
    /// The function of this feature should be transparent to the user, and thus no configuration is needed.
    /// </summary>
    class EntityCache : BotFeature
    {
        private readonly DatabaseConfig _db;

        public override string Name => nameof(EntityCache);
        
        public EntityCache(DiscordSocketClient client) : base(client)
        {
            _db = RegexBot.Config.Database;

            if (_db.Enabled)
            {
                client.GuildAvailable += Client_GuildAvailable;
                client.GuildUpdated += Client_GuildUpdated;
                client.GuildMemberUpdated += Client_GuildMemberUpdated;
                // it may not be necessary to handle JoinedGuild, as GuildAvailable provides this info
            }
            else
            {
                Log("No database storage available.").Wait();
            }
        }
        
        public override Task<object> ProcessConfiguration(JToken configSection) => Task.FromResult<object>(null);

        #region Event handling
        // Guild _and_ guild member information has become available
        private async Task Client_GuildAvailable(SocketGuild arg)
        {
            await CreateCacheTables(arg.Id);

            await Task.Run(() => UpdateGuild(arg));
            await Task.Run(() => UpdateGuildMember(arg.Id, arg.Users));
        }

        // Guild information has changed
        private async Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            await Task.Run(() => UpdateGuild(arg2));
        }

        // Guild member information has changed
        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            await Task.Run(() => UpdateGuildMember(arg2));
        }
        #endregion

        #region Table setup
        public const string TableGuild = "cache_guild";
        const string TableUser = "cache_users";

        private async Task CreateCacheTables(ulong gid)
        {
            using (var db = await _db.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableGuild + "("
                        + "guild_id bigint primary key, "
                        + "current_name text not null, "
                        + "display_name text null"
                        + ")";
                    // TODO determine if other columns necessary?
                    await c.ExecuteNonQueryAsync();
                }

                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableUser + "("
                        + "user_id bigint, "
                        + "guild_id bigint references " + TableGuild + " (guild_id), "
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
                    c.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS "
                        + $"{TableUser}_idx on {TableUser} (user_id, guild_id)";
                    await c.ExecuteNonQueryAsync();
                }
            }
        }
        #endregion
        
        private async Task UpdateGuild(SocketGuild g)
        {
            using (var db = await _db.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "INSERT INTO " + TableGuild + " (guild_id, current_name) "
                        + "(@GuildId, @CurrentName) "
                        + "ON CONFLICT (guild_id) DO UPDATE SET "
                        + "current_name = EXCLUDED.current_name";
                    c.Parameters.Add("@GuildID", NpgsqlDbType.Bigint).Value = g.Id;
                    c.Parameters.Add("@CurrentName", NpgsqlDbType.Text).Value = g.Name;
                    c.Prepare();
                    await c.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateGuildMember(ulong gid, IEnumerable<SocketGuildUser> users)
        {
            using (var db = await _db.GetOpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "INSERT INTO " + TableUser + " VALUES "
                        + "(@Uid, @Gid, @Date, @Uname, @Disc, @Nname, @Url) "
                        + "ON CONFLICT (user_id, guild_id) DO UPDATE SET "
                        + "cache_date = EXCLUDED.cache_date, username = EXCLUDED.username, "
                        + "discriminator = EXCLUDED.discriminator, " // I've seen someone's discriminator change this one time...
                        + "nickname = EXCLUDED.nickname, avatar_url = EXCLUDED.avatar_url";
                    c.Prepare();

                    var now = DateTime.Now;
                    List<Task> inserts = new List<Task>();

                    foreach (var item in users)
                    {
                        c.Parameters.Clear();
                        c.Parameters.Add("@Uid", NpgsqlDbType.Bigint).Value = item.Id;
                        c.Parameters.Add("@Gid", NpgsqlDbType.Bigint).Value = item.Guild.Id;
                        c.Parameters.Add("@Date", NpgsqlDbType.TimestampTZ).Value = now;
                        c.Parameters.Add("@Uname", NpgsqlDbType.Text).Value = item.Username;
                        c.Parameters.Add("@Disc", NpgsqlDbType.Text).Value = item.Discriminator;
                        c.Parameters.Add("@Nname", NpgsqlDbType.Text).Value = item.Nickname;
                        c.Parameters.Add("@Url", NpgsqlDbType.Text).Value = item.GetAvatarUrl();
                        await c.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private Task UpdateGuildMember(SocketGuildUser user)
        {
            var gid = user.Guild.Id;
            var ml = new SocketGuildUser[] { user };
            return UpdateGuildMember(gid, ml);
        }
    }
}
