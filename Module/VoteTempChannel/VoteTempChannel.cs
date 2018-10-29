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
            var conf = GetState<GuildInformation>(guild.Id);
            if (conf == null) return;

            // Only check the designated voting channel
            if (!string.Equals(arg.Channel.Name, conf.Config.VotingChannel,
                StringComparison.InvariantCultureIgnoreCase)) return;

            // Check if command invoked
            if (!arg.Content.StartsWith(conf.Config.VoteCommand, StringComparison.InvariantCultureIgnoreCase)) return;

            // Check if we're accepting votes. Locking here; background task may be using this.
            bool cooldown;
            bool voteCounted = false;
            string newChannelName = null;
            lock (conf)
            {
                if (conf.GetTemporaryChannel(guild) != null) return; // channel exists, nothing to vote for
                cooldown = conf.Voting.IsInCooldown();
                if (!cooldown)
                {
                    voteCounted = conf.Voting.AddVote(arg.Author.Id, out var voteCount);
                    if (voteCount >= conf.Config.VotePassThreshold)
                    {
                        newChannelName = conf.Config.TempChannelName;
                    }
                }

                // Prepare new temporary channel while we're still locking state
                if (newChannelName != null) conf.TempChannelLastActivity = DateTime.UtcNow;
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

            await arg.Channel.SendMessageAsync(":white_check_mark: Channel creation vote has been counted."
                    + (newChannelName != null ? $"\n<#{newChannelName}> has been created!" : ""));
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
            var conf = GetState<GuildInformation>(guild.Id);
            if (conf == null) return Task.CompletedTask;

            lock (conf)
            {
                var tch = conf.GetTemporaryChannel(guild);
                if (arg.Channel.Name == tch.Name)
                {
                    conf.TempChannelLastActivity = DateTimeOffset.UtcNow;
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
