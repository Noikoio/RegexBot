using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
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
        }
        
        private async Task GuildEnter(SocketGuild arg)
        {
            var conf = GetState<GuildConfiguration>(arg.Id);
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

        private async Task HandleVote_TempChannelNotExists(SocketMessage arg, SocketGuild guild, GuildConfiguration conf, int voteCount)
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

        private async Task HandleVote_TempChannelExists(SocketMessage arg, SocketGuild guild, GuildConfiguration conf, int voteCount)
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
                return Task.FromResult<object>(new GuildConfiguration((JObject)configSection));
            }
            throw new RuleImportException("Configuration not of a valid type.");
        }

        /// <summary>
        /// Publicly accessible method for fetching config. Used by <see cref="ChannelManager"/>.
        /// </summary>
        public GuildConfiguration GetConfig(ulong guildId) => GetState<GuildConfiguration>(guildId);
        // TODO check if used ^. attempt to not use.
    }
}
