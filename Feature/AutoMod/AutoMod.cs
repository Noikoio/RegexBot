using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.Feature.AutoMod.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoMod
{
    /// <summary>
    /// Implements per-message regex matching and executes customizable responses.
    /// The name RegexBot comes from the existence of this feature.
    /// </summary>
    /// <remarks>
    /// Strictly for use as a moderation tool only. Triggers that simply reply to messages
    /// should be implemented using <see cref="AutoRespond"/>.
    /// </remarks>
    class AutoMod : BotFeature
    {
        public override string Name => "AutoMod";

        public AutoMod(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += CMessageReceived;
            client.MessageUpdated += CMessageUpdated;
        }

        [ConfigSection("automod")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            List<Rule> rules = new List<Rule>();
            foreach (JObject ruleconf in configSection)
            {
                var rule = new Rule(this, ruleconf);
                rules.Add(rule);
                await Log($"Added rule '{rule.Label}'");
            }
            return rules.AsReadOnly();
        }

        private async Task CMessageReceived(SocketMessage arg)
            => await ReceiveMessage(arg);
        private async Task CMessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
            => await ReceiveMessage(arg2);

        /// <summary>
        /// Does initial message checking before sending to further processing.
        /// </summary>
        private async Task ReceiveMessage(SocketMessage m)
        {
            // Determine if incoming channel is in a guild
            var ch = m.Channel as SocketGuildChannel;
            if (ch == null) return;

            // Get rules
            var rules = GetConfig(ch.Guild.Id) as IEnumerable<Rule>;
            if (rules == null) return;

            foreach (var rule in rules)
            {
                // Checking for mod bypass here (Rule.Match isn't able to access mod list)
                bool isMod = IsModerator(ch.Guild.Id, m);
                await Task.Run(async () => await ProcessMessage(m, rule, isMod));
            }
        }

        /// <summary>
        /// Checks if the incoming message matches the given rule, and executes responses if necessary. 
        /// </summary>
        private async Task ProcessMessage(SocketMessage m, Rule r, bool isMod)
        {
            if (!r.Match(m, isMod)) return;

            // TODO make log optional; configurable
            await Log($"{r} triggered by {m.Author.ToString()}");

            foreach (Response resp in r.Response)
            {
                try
                {
                    await resp.Invoke(m);
                }
                catch (Exception ex)
                {
                    await Log($"Encountered an error while processing '{resp.CmdArg0}'. Details follow:");
                    await Log(ex.ToString());
                }
            }
        }

        public new Task Log(string text) => base.Log(text);
        public new DiscordSocketClient Client => base.Client;
    }
}
