using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace Noikoio.RegexBot.Feature.DBCache
{
    /// <summary>
    /// Caches information regarding all incoming messages and all known guilds, channels, and users.
    /// The function of this feature should be transparent to the user, and thus no configuration is needed.
    /// </summary>
    class DBCache : BotFeature
    {
        public override string Name => "Database cache";
        
        public DBCache(DiscordSocketClient client) : base(client)
        {
            client.GuildAvailable += Client_GuildAvailable;
            client.GuildUpdated += Client_GuildUpdated;
            client.GuildMemberUpdated += Client_GuildMemberUpdated;

            client.MessageReceived += Client_MessageReceived;
            client.MessageUpdated += Client_MessageUpdated;
        }

        public override Task<object> ProcessConfiguration(JToken configSection) => Task.FromResult<object>(null);

        #region Guild and user cache
        /* Note:
         * We save information per guild in their own schemas named "g_NUM", where NUM is the Guild ID.
         * 
         * The creation of these schemas is handled within here, but we're possibly facing a short delay
         * in the event that other events that we're listening for come in without a schema having been
         * created yet in which to put them in.
         * Got to figure that out.
         */

        // Guild information has changed
        private Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            throw new NotImplementedException();
        }

        // Single guild member information has changed
        private Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            // which is old and which is new?
            throw new NotImplementedException();
        }

            // All member data in a guild has become known
        private Task Client_GuildAvailable(SocketGuild arg)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Message cache
        private Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            throw new NotImplementedException();
        }

        private Task Client_MessageReceived(SocketMessage arg)
        {
            throw new NotImplementedException();
        }
        #endregion


    }
}
