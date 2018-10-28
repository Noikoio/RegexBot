using Discord.Rest;
using Discord.WebSocket;
using Npgsql;
using NpgsqlTypes;
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
        /// Key = guild, Value = expiry time, notify flag.
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
            SetUpPersistenceTableAsync().Wait();
        }

        public void Dispose()
        {
            _token.Cancel();
            _token.Dispose();
        }

        #region Data persistence
        private const string PersistTable = "votetemp_persist";
        private async Task SetUpPersistenceTableAsync()
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"create table if not exists {PersistTable} (" +
                        "guild_id bigint primary key, " +
                        "expiration_time timestamptz not null" +
                        ")";
                    await c.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<DateTimeOffset?> GetPersistData(ulong guildId)
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"select expiration_time from {PersistTable} where guild_id = @Gid";
                    c.Parameters.Add("@Gid", NpgsqlDbType.Bigint).Value = guildId;
                    c.Prepare();
                    using (var r = await c.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync()) return r.GetDateTime(0);
                        return null;
                    }
                }
            }
        }

        private async Task InsertOrUpdatePersistData(ulong guildId, DateTimeOffset expiration)
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"delete from {PersistTable} where guild_id = @Gid";
                    c.Parameters.Add("@Gid", NpgsqlDbType.Bigint).Value = guildId;
                    await c.ExecuteNonQueryAsync();
                }

                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"insert into {PersistTable} values (@Gid, @Exp)";
                    c.Parameters.Add("@Gid", NpgsqlDbType.Bigint).Value = guildId;
                    c.Parameters.Add("@Exp", NpgsqlDbType.TimestampTZ).Value = expiration;
                    c.Prepare();
                    try
                    {
                        await c.ExecuteNonQueryAsync();
                    }
                    catch (NpgsqlException ex)
                    {
                        // TODO should log this instead of throwing an exception...
                        throw new ApplicationException("A database error occurred. Internal error message: " + ex.Message);
                    }
                }
            }
        }

        private async Task DeletePersistData(ulong guildId)
        {
            using (var db = await RegexBot.Config.GetOpenDatabaseConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = $"delete from {PersistTable} where guild_id = @Gid";
                    c.Parameters.Add("@Gid", NpgsqlDbType.Bigint).Value = guildId;
                    c.Prepare();
                    await c.ExecuteNonQueryAsync();
                }
            }
        }
        #endregion

        #region Querying
        /// <summary>
        /// Determines if the given guild has a temporary channel that is up for a renewal vote.
        /// </summary>
        public bool IsUpForRenewal(SocketGuild guild, GuildConfiguration info)
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

        private SocketTextChannel FindTemporaryChannel(SocketGuild guild, GuildConfiguration conf)
            => System.Linq.Enumerable.SingleOrDefault(guild.TextChannels, c => c.Name == conf.TempChannelName);

        public bool HasExistingTemporaryChannel(SocketGuild guild, GuildConfiguration info)
        {
            return FindTemporaryChannel(guild, info) != null;
        }
        #endregion

        #region Channel entry manipulation
        /// <summary>
        /// Creates the temporary channel.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Various causes. Send exception message to log and channel if thrown.
        /// </exception>
        public async Task<RestTextChannel> CreateChannelAndEntryAsync(SocketGuild guild, GuildConfiguration info)
        {
            lock (_trackedChannels)
            {
                // Disregard if already in cache. (How did we get here?)
                if (_trackedChannels.ContainsKey(guild.Id)) return null;
            }

            var channelExpiryTime = DateTimeOffset.UtcNow + info.ChannelBaseDuration;

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

            // Create persistent entry.
            await InsertOrUpdatePersistData(guild.Id, channelExpiryTime);

            return newCh;
        }

        /// <summary>
        /// For an existing temporary channel, extends its lifetime by a predetermined amount.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// SQL. Send exception message to log and channel if thrown.
        /// </exception>
        public async Task ExtendChannelExpirationAsync(SocketGuild guild, GuildConfiguration info)
        {
            DateTimeOffset newExpiration;
            lock (_trackedChannels)
            {
                if (!_trackedChannels.ContainsKey(guild.Id)) return; // how did we even get here?

                newExpiration = _trackedChannels[guild.Id].Item1;
                newExpiration+= info.ChannelExtendDuration;
                _trackedChannels[guild.Id] = (newExpiration, false);
            }

            await InsertOrUpdatePersistData(guild.Id, newExpiration);
        }

        /// <summary>
        /// Called when becoming aware of a new guild. Checks and acts on persistence data.
        /// </summary>
        public async Task RecheckExpiryInformation(SocketGuild guild, GuildConfiguration info)
        {
            var ch = FindTemporaryChannel(guild, info);
            var persist = await GetPersistData(guild.Id);

            if (persist.HasValue)
            {
                // Found persistence data and...
                if (ch == null)
                {
                    // ...there is no existing corresponding channel. Delete persistence data.
                    await DeletePersistData(guild.Id);
                }
                else
                {
                    // ...the channel exists. Add to in-memory cache.
                    // Cached persistence should extend to at least 5 minutes if needed.
                    // Should allow for enough time for users to vote for an extension.
                    DateTimeOffset toCache;
                    if ((DateTimeOffset.UtcNow - persist.Value).Duration().TotalMinutes > 5)
                        toCache = persist.Value;
                    else
                        toCache = DateTimeOffset.UtcNow.AddMinutes(5);
                    lock (_trackedChannels) { _trackedChannels.Add(guild.Id, (toCache, false)); }
                }
            }
            else
            {
                // No persistence data.
                if (ch != null)
                {
                    // But we have a channel. Add new value to cache.
                    var exp = DateTimeOffset.UtcNow + info.ChannelBaseDuration;
                    lock (_trackedChannels) { _trackedChannels.Add(guild.Id, (exp, false)); }
                    await InsertOrUpdatePersistData(guild.Id, exp);
                }
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
            await DeletePersistData(guild.Id);
        }

        /// <summary>
        /// Removes the given guild from the cache. Does not alter persistence data.
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
                            DeletePersistData(item.Key).Wait();
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
