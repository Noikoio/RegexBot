using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Keeps track of existing channels and expiry information. Manages data persistence.
    /// </summary>
    class ChannelManager : IDisposable
    {
        readonly VoteTempChannel _out;
        readonly DiscordSocketClient _client;

        /// <summary>
        /// Key = guild, Value = expiry time
        /// Must lock!
        /// </summary>
        readonly Dictionary<ulong, (DateTimeOffset, bool)> _trackedChannels;

        readonly CancellationTokenSource _token;
        readonly Task _bgTask;

        public ChannelManager(VoteTempChannel module, DiscordSocketClient client)
        {
            _out = module;
            _client = client;
            _token = new CancellationTokenSource();
            _bgTask = Task.Factory.StartNew(ChannelExpirationChecker, _token.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _trackedChannels = new Dictionary<ulong, (DateTimeOffset, bool)>();
        }

        public void Dispose()
        {
            _token.Cancel();
            _token.Dispose();
        }
        
        #region Querying
        /// <summary>
        /// Determines if the given guild has a temporary channel that is up for a renewal vote.
        /// </summary>
        public bool IsUpForRenewal(SocketGuild guild, Configuration info)
        {
            DateTimeOffset tcExp;
            lock (_trackedChannels)
            {
                if (!_trackedChannels.TryGetValue(guild.Id, out var val)) return false;
                tcExp = val.Item1;
            }

            var renewThreshold = tcExp - info.KeepaliveVoteDuration;
            return DateTimeOffset.UtcNow > renewThreshold;
        }
        
        #endregion

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

            // Channel creation succeeded. Regardless of persistent state, at least add it to in-memory cache.
            lock (_trackedChannels) _trackedChannels.Add(guild.Id, (channelExpiryTime, false));

            return newCh;
        }

        /// <summary>
        /// For an existing temporary channel, extends its lifetime by a predetermined amount.
        /// </summary>
        public async Task ExtendChannelExpirationAsync(SocketGuild guild, Configuration info)
        {
            DateTimeOffset newExpiration;
            lock (_trackedChannels)
            {
                if (!_trackedChannels.ContainsKey(guild.Id)) return; // how did we even get here?

                newExpiration = _trackedChannels[guild.Id].Item1;
                newExpiration+= info.ChannelExtendDuration;
                _trackedChannels[guild.Id] = (newExpiration, false);
            }
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

        /// <summary>
        /// Removes the given guild from the cache.
        /// </summary>
        public void DropCacheEntry(SocketGuild guild)
        {
            lock (_trackedChannels) _trackedChannels.Remove(guild.Id);
        }
        #endregion

        /// <summary>
        /// Background task. Handles channel deletion on expiry.
        /// </summary>
        private async Task ChannelExpirationChecker()
        {
            while (!_token.Token.IsCancellationRequested)
            {
                lock (_trackedChannels)
                {
                    var now = DateTimeOffset.UtcNow;
                    var cachePostRemove = new List<ulong>(); // list of items to remove; can't remove while iterating
                    var cacheWarnSet = new List<ulong>(); // list of items to update the announce flag; can't change while iterating
                    foreach (var item in _trackedChannels)
                    {
                        var g = _client.GetGuild(item.Key);
                        if (g == null)
                        {
                            // Cached guild is not known, somehow...
                            cachePostRemove.Add(item.Key);
                            continue;
                        }

                        var conf = _out.GetConfig(item.Key);
                        if (conf == null)
                        {
                            // Cached guild has no config, somehow...
                            cachePostRemove.Add(item.Key);
                            continue;
                        }

                        var ch = FindTemporaryChannel(g, conf);
                        if (ch == null)
                        {
                            // Temporary channel no longer exists.
                            // Assume it's been deleted early, but do not start a cooldown.
                            cachePostRemove.Add(item.Key);
                            continue;
                        }

                        if (now > item.Value.Item1)
                        {
                            // Process channel removal
                            try
                            {
                                ch.DeleteAsync().Wait();
                                _out._votes.SetCooldown(ch.Guild.Id);
                            }
                            catch (Discord.Net.HttpException)
                            {
                                // On deletion error, attempt to report the issue. Discard from cache.
                                try
                                {
                                    ch.SendMessageAsync("Warning: Unable to remove temporary channel. It must now be done manually.");
                                }
                                catch (Discord.Net.HttpException) { }
                                cachePostRemove.Add(item.Key);
                                continue;
                            }
                            cachePostRemove.Add(item.Key);
                        }
                        else if (item.Value.Item2 == false && IsUpForRenewal(ch.Guild, conf))
                        {
                            // Process channel renewal warning
                            ch.SendMessageAsync("This channel is nearing expiration! Vote to extend it by issuing " +
                                $"the `{conf.VoteCommand}` command.").Wait();
                            cacheWarnSet.Add(item.Key);
                        }
                    }
                    foreach (var guildId in cachePostRemove)
                    {
                        _trackedChannels.Remove(guildId);
                    }
                    foreach (var id in cacheWarnSet)
                    {
                        var newdata = (_trackedChannels[id].Item1, true);
                        _trackedChannels.Remove(id);
                        _trackedChannels.Add(id, newdata);
                    }
                }

                try { await Task.Delay(12 * 1000, _token.Token); }
                catch (TaskCanceledException) { break; }
            }
        }
    }
}
