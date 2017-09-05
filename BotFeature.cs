using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Base class for bot features
    /// </summary>
    /// <remarks>
    /// This may have use in some sort of external plugin system later.
    /// </remarks>
    abstract class BotFeature
    {
        private readonly DiscordSocketClient _client;
        private readonly AsyncLogger _logger;
        
        public abstract string Name { get; }
        protected DiscordSocketClient Client => _client;

        protected BotFeature(DiscordSocketClient client)
        {
            _client = client;
            _logger = Logger.GetLogger(this.Name);
        }

        /// <summary>
        /// Processes feature-specific configuration.
        /// </summary>
        /// <remarks>
        /// Feature code <i>should not</i> hold on to this data, but instead use <see cref="GetConfig(ulong)"/> to retrieve
        /// them. This is in the event that configuration is reverted to an earlier state and allows for the
        /// bot and all features to revert to previously used configuration values with no effort on the part
        /// of individual features.
        /// </remarks>
        /// <returns>
        /// Processed configuration data prepared for later use.
        /// </returns>
        /// <exception cref="ConfigItem.RuleImportException">
        /// This method should throw RuleImportException in the event of any error.
        /// The exception message will be properly logged.
        /// </exception>
        public abstract Task<object> ProcessConfiguration(JToken configSection);

        /// <summary>
        /// Gets this feature's relevant configuration data associated with the given Discord guild.
        /// </summary>
        /// <returns>
        /// The stored configuration data, or null if none exists.
        /// </returns>
        protected object GetConfig(ulong guildId)
        {
            var sc = RegexBot.Config.Servers.FirstOrDefault(g => g.Id == guildId);
            if (sc == null)
            {
                throw new ArgumentException("There is no known configuration associated with the given Guild ID.");
            }

            if (sc.FeatureConfigs.TryGetValue(this, out var item)) return item;
            else return null;
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

    /// <summary>
    /// Indicates which section under an individual Discord guild configuration should be passed to the
    /// feature's <see cref="BotFeature.ProcessConfiguration(JToken)"/> method during configuration load.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ConfigSectionAttribute : Attribute
    {
        private readonly string _sectionName;

        public string SectionName => _sectionName;

        public ConfigSectionAttribute(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                throw new ArgumentNullException("Configuration section name cannot be blank.");
            }
            _sectionName = sectionName;
        }
    }
}
