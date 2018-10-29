using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Keeps track of existing channels and expiry information. Manages data persistence.
    /// </summary>
    class ChannelManager
    {
        readonly VoteTempChannel _out;
        readonly DiscordSocketClient _client;

        readonly CancellationTokenSource _token;
        readonly Task _bgTask;

        public ChannelManager(VoteTempChannel module, DiscordSocketClient client)
        {
            _out = module;
            _client = client;
        }

        #region Channel entry manipulation
        /// <summary>
        /// Creates the temporary channel.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Various causes. Send exception message to log and channel if thrown.
        /// </exception>
        public async Task<RestTextChannel> CreateChannelAndEntryAsync(SocketGuild guild, Configuration info)
        {
            lock (_trackedChannels)
            {
                // Disregard if already in cache. (How did we get here?)
                if (_trackedChannels.ContainsKey(guild.Id)) return null;
            }

            RestTextChannel newCh = null;
            try
            {
                newCh = await guild.CreateTextChannelAsync(info.TempChannelName);
            }
            catch (Discord.Net.HttpException ex)
            {
                throw new ApplicationException("Failed to create the channel. Internal error message: " + ex.Message);
            }

            return newCh;
        }
        
        /// <summary>
        /// Sets the given guild's temporary channel as up for immediate expiration.
        /// Use this to properly remove a temporary channel.
        /// </summary>
        public async Task SetChannelEarlyExpiry(SocketGuild guild)
        {
            lock (_trackedChannels)
            {
                if (!_trackedChannels.ContainsKey(guild.Id)) return; // how did we even get here?
                _trackedChannels[guild.Id] = (DateTimeOffset.UtcNow, true);
            }
        }
        #endregion
        
    }
}
