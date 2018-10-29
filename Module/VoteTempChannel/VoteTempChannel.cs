using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// "Entry point" for VoteTempChannel feature.
    /// Handles activation command depending on guild state. Also holds information on
    /// temporary channels currently active.
    /// </summary>
    class VoteTempChannel : BotModule
    {
        Task _backgroundWorker;
        CancellationTokenSource _backgroundWorkerCancel;
        ChannelManager _chMgr;
        internal VoteStore _votes;

        public VoteTempChannel(DiscordSocketClient client) : base(client)
        {
            _chMgr = new ChannelManager(this, client);
            _votes = new VoteStore();

            client.JoinedGuild += GuildEnter;
            client.GuildAvailable += GuildEnter;
            client.LeftGuild += GuildLeave;
            client.MessageReceived += Client_MessageReceived;

            _backgroundWorkerCancel = new CancellationTokenSource();
            _backgroundWorker = Task.Factory.StartNew(BackgroundCheckingTask, _backgroundWorkerCancel.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        
        private async Task GuildEnter(SocketGuild arg)
        {
            var conf = GetState<Configuration>(arg.Id);
            if (conf != null) await _chMgr.RecheckExpiryInformation(arg, conf);
        }

        private Task GuildLeave(SocketGuild arg)
        {
            _chMgr.DropCacheEntry(arg);
            return Task.CompletedTask;
        }

        // Handles all vote logic
        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot) return;
            if (arg.Channel is IDMChannel) return;
            var guild = (arg.Channel as SocketTextChannel)?.Guild;
            if (guild == null) return;
            var conf = GetConfig(guild.Id);
            if (conf == null) return;

            if (!arg.Content.StartsWith(conf.VoteCommand, StringComparison.InvariantCultureIgnoreCase)) return;

            var voteResult = _votes.AddVote(guild.Id, arg.Author.Id, out int voteCount);
            if (voteResult == VoteStatus.FailCooldown)
            {
                await arg.Channel.SendMessageAsync(":x: Cooldown in effect. Try again later.");
                return;
            }

            const string VoteError = ":x: You have already placed your vote.";

            if (_chMgr.HasExistingTemporaryChannel(guild, conf))
            {
                // Ignore votes not coming from the temporary channel itself.
                if (!string.Equals(arg.Channel.Name, conf.TempChannelName, StringComparison.InvariantCultureIgnoreCase))
                {
                    _votes.DelVote(guild.Id, arg.Author.Id);
                    return;
                }
                if (voteResult == VoteStatus.FailVotedAlready)
                {
                    await arg.Channel.SendMessageAsync(VoteError);
                    return;
                }
                await HandleVote_TempChannelExists(arg, guild, conf, voteCount);
            }
            else
            {
                if (voteResult == VoteStatus.FailVotedAlready)
                {
                    await arg.Channel.SendMessageAsync(VoteError);
                    return;
                }
                await HandleVote_TempChannelNotExists(arg, guild, conf, voteCount);
            }
        }

        private async Task HandleVote_TempChannelNotExists(SocketMessage arg, SocketGuild guild, Configuration conf, int voteCount)
        {
            bool threshold = voteCount >= conf.VotePassThreshold;
            RestTextChannel newCh = null;

            if (threshold)
            {
                newCh = await _chMgr.CreateChannelAndEntryAsync(guild, conf);
                _votes.ClearVotes(guild.Id);
            }

            await arg.Channel.SendMessageAsync(":white_check_mark: Channel creation vote has been counted."
                + (threshold ? $"\n<#{newCh.Id}> is now available!" : ""));
            if (newCh != null)
                await newCh.SendMessageAsync($"Welcome to <#{newCh.Id}>!"
                    + "\nPlease note that this channel is temporary and *will* be deleted at a later time.");
        }

        private async Task HandleVote_TempChannelExists(SocketMessage arg, SocketGuild guild, Configuration conf, int voteCount)
        {
            // It's been checked that the incoming message originated from the temporary channel itself before coming here.
            if (!_chMgr.IsUpForRenewal(guild, conf))
            {
                // TODO consider changing 'renewal' to 'extension' in other references, because the word makes more sense
                if (conf.ChannelExtendDuration != TimeSpan.Zero)
                    await arg.Channel.SendMessageAsync(":x: Cannot currently vote for a time extension. Try again later.");
                else
                    await arg.Channel.SendMessageAsync(":x: This channel's duration may not be extended.");
                _votes.ClearVotes(guild.Id);
                return;
            }

            bool threshold = voteCount >= conf.VotePassThreshold;
            if (threshold)
            {
                _votes.ClearVotes(guild.Id);
                await _chMgr.ExtendChannelExpirationAsync(guild, conf);
            }

            await arg.Channel.SendMessageAsync(":white_check_mark: Extension vote has been counted."
                + (threshold ? "\nThis channel's duration has been extended." : ""));
        }

        public override Task<object> CreateInstanceState(JToken configSection)
        {
            if (configSection == null) return Task.FromResult<object>(null);
            if (configSection.Type == JTokenType.Object)
            {
                return Task.FromResult<object>(new GuildInformation((JObject)configSection));
            }
            throw new RuleImportException("Configuration not of a valid type.");
        }

        #region Background tasks
        /// <summary>
        /// Two functions: Removes expired channels, and announces expired votes.
        /// </summary>
        private async Task BackgroundCheckingTask()
        {
            while (!_backgroundWorkerCancel.IsCancellationRequested)
            {
                try { await Task.Delay(12000, _backgroundWorkerCancel.Token); }
                catch (TaskCanceledException) { return; }
                
                foreach (var g in Client.Guilds)
                {
                    try
                    {
                        var conf = GetState<GuildInformation>(g.Id);
                        if (conf == null) continue;

                        await BackgroundTempChannelExpiryCheck(g, conf);
                        await BackgroundVoteSessionExpiryCheck(g, conf);
                    }
                    catch (Exception ex)
                    {
                        Log("Unhandled exception in background task when processing a single guild.").Wait();
                        Log(ex.ToString()).Wait();
                    }
                }
            }
        }

        private async Task BackgroundTempChannelExpiryCheck(SocketGuild g, GuildInformation conf)
        {
            SocketGuildChannel ch = null;
            lock (conf)
            {
                ch = conf.GetTemporaryChannel(g);
                if (ch == null) return; // No temporary channel. Nothing to do.
                if (!conf.IsTempChannelExpired()) return;

                // If we got this far, the channel's expiring. Start the voting cooldown.
                conf.Voting.StartCooldown();
            }
            await ch.DeleteAsync();
        }

        private async Task BackgroundVoteSessionExpiryCheck(SocketGuild g, GuildInformation conf)
        {
            bool act;
            string nameTest;
            lock (conf) {
                act = conf.Voting.IsSessionExpired();
                nameTest = conf.Config.VotingChannel;
            }

            if (!act) return;
            // Determine the voting channel; will send announcement there.
            SocketTextChannel outCh = null;
            foreach (var ch in g.TextChannels)
            {
                if (string.Equals(ch.Name, nameTest, StringComparison.InvariantCultureIgnoreCase))
                {
                    outCh = ch;
                    break;
                }
            }
            if (outCh == null)
            {
                // Huh. Bad config?
                return;
            }
            await outCh.SendMessageAsync(":x: Not enough votes were placed for channel creation.");
        }
        #endregion
    }
}
