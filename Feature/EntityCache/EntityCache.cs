using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.EntityCache
{
    /// <summary>
    /// Caches information regarding all known guilds, channels, and users.
    /// The function of this feature should be transparent to the user, and thus no configuration is needed.
    /// This feature should be initialized BEFORE any other features that make use of guild and user cache.
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
                Sql.CreateCacheTables();

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
            await Task.Run(async () =>
            {
                await UpdateGuild(arg);
                await UpdateGuildMember(arg.Users);
            }
            );
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
        
#endregion
        
        private async Task UpdateGuild(SocketGuild g)
        {
            try
            {
                using (var db = await _db.GetOpenConnectionAsync())
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
            catch (NpgsqlException ex)
            {
                await Log($"SQL error in {nameof(UpdateGuild)}: " + ex.Message);
            }
        }

        private async Task UpdateGuildMember(IEnumerable<SocketGuildUser> users)
        {
            try
            {
                using (var db = await _db.GetOpenConnectionAsync())
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
            catch (NpgsqlException ex)
            {
                await Log($"SQL error in {nameof(UpdateGuildMember)}: " + ex.Message);
            }
        }

        private Task UpdateGuildMember(SocketGuildUser user)
        {
            var gid = user.Guild.Id;
            var ml = new SocketGuildUser[] { user };
            return UpdateGuildMember(ml);
        }
    }
}
