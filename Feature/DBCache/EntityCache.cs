using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
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

            client.GuildAvailable += Client_GuildAvailable;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildMemberUpdated += Client_GuildMemberUpdated;
            // it may not be necessary to handle JoinedGuild, as GuildAvailable still provides info
        }
        
        public override Task<object> ProcessConfiguration(JToken configSection) => Task.FromResult<object>(null);

        #region Event handling
        // Guild _and_ guild member information has become available
        private async Task Client_GuildAvailable(SocketGuild arg)
        {
            if (!_db.Enabled) return;
            await CreateCacheTables(arg.Id);

            await Task.Run(() => UpdateGuild(arg));
            await Task.Run(() => UpdateGuildMember(arg.Id, arg.Users));
        }

        // Guild information has changed
        private async Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            if (!_db.Enabled) return;
            await Task.Run(() => UpdateGuild(arg2));
        }

        // Guild member information has changed
        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            if (!_db.Enabled) return;
            await Task.Run(() => UpdateGuildMember(arg2));
        }
        #endregion

        #region Table setup
        const string TableGuild = "cache_guild";
        const string TableUser = "cache_users";

        private async Task CreateCacheTables(ulong gid)
        {
            /* Note:
             * We save information per guild in their own schemas named "g_NUM", where NUM is the Guild ID.
             * 
             * The creation of these schemas is handled within here, but we're possibly facing a short delay
             * in the event that other events that we're listening for come in without a schema having been
             * created yet in which to put them in.
             * Got to figure that out.
             */
            await _db.CreateGuildSchemaAsync(gid);

            using (var db = await _db.OpenConnectionAsync(gid))
            {
                Task<int> c1, c2;

                // Uh... didn't think this through. For now this is a table that'll only ever have one column.
                // Got to rethink this in particular.
                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableGuild + "("
                        + "snowflake bigint primary key, "
                        + "current_name text not null, "
                        + "display_name text null"
                        + ")";
                    c1 = c.ExecuteNonQueryAsync();
                }

                using (var c = db.CreateCommand())
                {
                    c.CommandText = "CREATE TABLE IF NOT EXISTS " + TableUser + "("
                        + "snowflake bigint primary key, "
                        + "cache_date timestamptz not null, "
                        + "username text not null, "
                        + "discriminator text not null, "
                        + "nickname text null, "
                        + "avatar_url text null"
                        + ")";
                    c2 = c.ExecuteNonQueryAsync();
                }

                await c1;
                await c2;
            }
        }
        #endregion
        
        private async Task UpdateGuild(SocketGuild g)
        {
            throw new NotImplementedException();
        }

        private async Task UpdateGuildMember(ulong gid, IEnumerable<SocketGuildUser> users)
        {
            throw new NotImplementedException();
        }

        private Task UpdateGuildMember(SocketGuildUser user)
        {
            var gid = user.Guild.Id;
            var ml = new SocketGuildUser[] { user };
            return UpdateGuildMember(gid, ml);
        }
    }
}
