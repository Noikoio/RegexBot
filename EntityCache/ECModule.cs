using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using Npgsql;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    /// <summary>
    /// Bot module portion of the entity cache. Caches information regarding all known guilds, channels, and users.
    /// The function of this module should be transparent to the user, and thus no configuration is needed.
    /// This module should be initialized BEFORE any other modules that make use of the entity cache.
    /// </summary>
    class ECModule : BotModule
    {
        private readonly DatabaseConfig _db;

        public ECModule(DiscordSocketClient client) : base(client)
        {
            if (RegexBot.Config.DatabaseAvailable)
            {
                SqlHelper.CreateCacheTablesAsync().Wait();

                client.GuildAvailable += Client_GuildAvailable;
                client.GuildUpdated += Client_GuildUpdated;
                client.GuildMemberUpdated += Client_GuildMemberUpdated;
                client.UserJoined += Client_UserJoined;
                client.UserLeft += Client_UserLeft;
            }
            else
            {
                Log("No database storage available.").Wait();
            }
        }
        
        // Guild and guild member information has become available.
        // This is a very expensive operation, especially when joining larger guilds.
        private async Task Client_GuildAvailable(SocketGuild arg)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await SqlHelper.UpdateGuildAsync(arg);
                    await SqlHelper.UpdateGuildMemberAsync(arg.Users);
                }
                catch (NpgsqlException ex)
                {
                    await Log($"SQL error in {nameof(Client_GuildAvailable)}: {ex.Message}");
                }
            });
        }

        // Guild information has changed
        private async Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await SqlHelper.UpdateGuildAsync(arg2);
                }
                catch (NpgsqlException ex)
                {
                    await Log($"SQL error in {nameof(Client_GuildUpdated)}: {ex.Message}");
                }
            });
        }

        // Guild member information has changed
        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            await Task.Run(async () => 
            {
                try
                {
                    await SqlHelper.UpdateGuildMemberAsync(arg2);
                }
                catch (NpgsqlException ex)
                {
                    await Log($"SQL error in {nameof(Client_GuildMemberUpdated)}: {ex.Message}");
                }
            });
        }

        // A new guild member has appeared
        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await SqlHelper.UpdateGuildMemberAsync(arg);
                }
                catch (NpgsqlException ex)
                {
                    await Log($"SQL error in {nameof(Client_UserJoined)}: {ex.Message}");
                }
            });
        }

        // User left the guild. No new data, but gives an excuse to update the cache date.
        private async Task Client_UserLeft(SocketGuildUser arg)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await SqlHelper.UpdateGuildMemberAsync(arg);
                }
                catch (NpgsqlException ex)
                {
                    await Log($"SQL error in {nameof(Client_UserLeft)}: {ex.Message}");
                }
            });
        }
    }
}
