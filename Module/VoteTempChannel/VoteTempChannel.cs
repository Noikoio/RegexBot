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
    /// Enables users to vote for the creation of a temporary channel.
    /// Deletes the channel after a set period of inactivity.
    /// </summary>
    class VoteTempChannel : BotModule
    {
        Task _backgroundWorker;
        CancellationTokenSource _backgroundWorkerCancel;

        public VoteTempChannel(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += VoteChecking;
            client.MessageReceived += TemporaryChannelActivityCheck;

            _backgroundWorkerCancel = new CancellationTokenSource();
            _backgroundWorker = Task.Factory.StartNew(BackgroundCheckingTask, _backgroundWorkerCancel.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
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

        /// <summary>
        /// Listens for voting commands.
        /// </summary>
        private async Task VoteChecking(SocketMessage arg)
        {
            if (arg.Author.IsBot) return;
            var guild = (arg.Channel as SocketTextChannel)?.Guild;
            if (guild == null) return;
            var info = GetState<GuildInformation>(guild.Id);
            if (info == null) return;

            // Only check the designated voting channel
            if (!string.Equals(arg.Channel.Name, info.Config.VoteChannel,
                StringComparison.InvariantCultureIgnoreCase)) return;

            // Check if command invoked
            if (!arg.Content.StartsWith(info.Config.VoteCommand, StringComparison.InvariantCultureIgnoreCase)) return;

            // Check if we're accepting votes. Locking here; other tasks may alter this data.
            bool cooldown;
            bool voteCounted = false;
            bool voteIsInitial = false;
            string newChannelName = null;
            lock (info)
            {
                if (info.GetTemporaryChannel(guild) != null) return; // channel exists, do nothing

                if (info.Voting.AwaitingInitialVote())
                {
                    // Vote not in effect. Ignore those not allowed to initiate a vote (if configured).
                    if (!info.Config.VoteStarters.IsEmpty() &&
                        !info.Config.VoteStarters.ExistsInList(arg)) return;
                }

                cooldown = info.Voting.IsInCooldown();
                if (!cooldown)
                {
                    voteCounted = info.Voting.AddVote(arg.Author.Id, out var voteCount);
                    voteIsInitial = voteCount == 1;
                    if (voteCount >= info.Config.VotePassThreshold)
                    {
                        // Non-null value in newChannelName signals vote success
                        newChannelName = info.Config.TempChannelName;
                    }
                }

                // Prepare some stuff while we're still in the lock
                if (newChannelName != null)
                {
                    info.TempChannelLastActivity = DateTime.UtcNow;
                    info.Voting.Reset();
                }
            }

            if (cooldown)
            {
                await arg.Channel.SendMessageAsync(":x: Cooldown in effect. Try again later.");
                return;
            }
            if (!voteCounted)
            {
                await arg.Channel.SendMessageAsync(":x: You have already voted.");
                return;
            }
            
            if (newChannelName != null)
            {
                RestTextChannel newCh;
                try
                {
                    newCh = await guild.CreateTextChannelAsync(newChannelName);
                }
                catch (Discord.Net.HttpException ex)
                {
                    await Log($"Failed to create temporary channel: {ex.Message}");
                    await arg.Channel.SendMessageAsync(":x: Failed to create new channel! Notify the bot operator.");
                    return;
                }

                await newCh.SendMessageAsync($"Welcome to <#{newCh.Id}>!"
                        + "\nBe aware that this channel is temporary and **will** be deleted later.");
                newChannelName = newCh.Id.ToString(); // For use in the confirmation message
            }
            if (voteIsInitial && newChannelName == null)
                await arg.Channel.SendMessageAsync($":white_check_mark: {arg.Author.Mention} has initiated a vote! " +
                    "Others may now vote to confirm creation of the new channel.");
            else
                await arg.Channel.SendMessageAsync(":white_check_mark: Channel creation vote has been counted."
                    + (newChannelName != null ? $"\n<#{newChannelName}> is now active!" : ""));
        }
        
        /// <summary>
        /// Listens for any message sent to the temporary channel.
        /// Updates the corresponding internal value.
        /// </summary>
        private Task TemporaryChannelActivityCheck(SocketMessage arg)
        {
            if (arg.Author.IsBot) return Task.CompletedTask;
            var guild = (arg.Channel as SocketTextChannel)?.Guild;
            if (guild == null) return Task.CompletedTask;
            var info = GetState<GuildInformation>(guild.Id);
            if (info == null) return Task.CompletedTask;

            lock (info)
            {
                var tch = info.GetTemporaryChannel(guild);
                if (tch == null) return Task.CompletedTask;
                if (arg.Channel.Name == tch.Name)
                {
                    info.TempChannelLastActivity = DateTimeOffset.UtcNow;
                }
            }

            return Task.CompletedTask;
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
                        var info = GetState<GuildInformation>(g.Id);
                        if (info == null) continue;

                        await BackgroundTempChannelExpiryCheck(g, info);
                        await BackgroundVoteSessionExpiryCheck(g, info);
                    }
                    catch (Exception ex)
                    {
                        Log("Unhandled exception in background task when processing a single guild.").Wait();
                        Log(ex.ToString()).Wait();
                    }
                }
            }
        }

        private async Task BackgroundTempChannelExpiryCheck(SocketGuild g, GuildInformation info)
        {
            SocketGuildChannel ch = null;
            lock (info)
            {
                ch = info.GetTemporaryChannel(g);
                if (ch == null) return; // No temporary channel. Nothing to do.
                if (!info.IsTempChannelExpired()) return;

                // If we got this far, the channel's expiring. Start the voting cooldown.
                info.Voting.StartCooldown();
            }
            await ch.DeleteAsync();
        }

        private async Task BackgroundVoteSessionExpiryCheck(SocketGuild g, GuildInformation info)
        {
            bool act;
            string nameTest;
            lock (info) {
                act = info.Voting.IsSessionExpired();
                nameTest = info.Config.VoteChannel;
                if (act) info.Voting.StartCooldown();
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
