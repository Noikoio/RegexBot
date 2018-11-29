using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Base class for bot modules
    /// </summary>
    abstract class BotModule
    {
        private readonly DiscordSocketClient _client;
        private readonly AsyncLogger _logger;

        public string Name => this.GetType().Name;
        protected DiscordSocketClient Client => _client;

        public BotModule(DiscordSocketClient client)
        {
            _client = client;
            _logger = Logger.GetLogger(this.Name);
        }

        /// <summary>
        /// This method is called on each module when configuration is (re)loaded.
        /// The module is expected to use this opportunity to set up an object that will hold state data
        /// for a particular guild, using the incoming configuration object as needed in order to do so.
        /// </summary>
        /// <remarks>
        /// Module code <i>should not</i> hold on state or configuration data on its own, but instead use
        /// <see cref="GetState{T}(ulong)"/> to retrieve its state object. This is to provide the user
        /// with the ability to maintain the current bot state in the event that a configuration reload fails.
        /// </remarks>
        /// <param name="configSection">
        /// Configuration data for this module, for this guild. Is null if none was defined.
        /// </param>
        /// <returns>An object that may later be retrieved by <see cref="GetState{T}(ulong)"/>.</returns>
        public virtual Task<object> CreateInstanceState(JToken configSection) => Task.FromResult<object>(null);

        /// <summary>
        /// Retrieves this module's relevant state data associated with the given Discord guild.
        /// </summary>
        /// <returns>
        /// The stored state data, or null/default if none exists.
        /// </returns>
        protected T GetState<T>(ulong guildId)
        {
            // TODO investigate if locking may be necessary
            var sc = RegexBot.Config.Servers.FirstOrDefault(g => g.Id == guildId);
            if (sc == null) return default(T);

            if (sc.ModuleConfigs.TryGetValue(this, out var item)) return (T)item;
            else return default(T);
        }

        /// <summary>
        /// Determines if the given message author or channel is in the server configuration's moderator list.
        /// </summary>
        protected bool IsModerator(ulong guildId, SocketMessage m)
        {
            var sc = RegexBot.Config.Servers.FirstOrDefault(g => g.Id == guildId);
            if (sc == null)
            {
                throw new ArgumentException("There is no known configuration associated with the given Guild ID.");
            }

            return sc.Moderators.ExistsInList(m);
        }
        
        protected async Task Log(string text)
        {
            await _logger(text);
        }
        
        public sealed override bool Equals(object obj) => base.Equals(obj);
        public sealed override int GetHashCode() => base.GetHashCode();
        public sealed override string ToString() => base.ToString();
    }
}
