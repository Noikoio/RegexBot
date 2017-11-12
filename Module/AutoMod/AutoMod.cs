using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.AutoMod
{
    /// <summary>
    /// Implements per-message regex matching and executes customizable responses.
    /// The name RegexBot comes from the existence of this feature.
    /// </summary>
    /// <remarks>
    /// Strictly for use as a moderation tool only. Triggers that simply reply to messages
    /// should be implemented using <see cref="AutoRespond"/>.
    /// </remarks>
    class AutoMod : BotModule
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
            List<ConfigItem> rules = new List<ConfigItem>();

            foreach (var def in configSection.Children<JProperty>())
            {
                string label = def.Name;
                var rule = new ConfigItem(this, def);
                rules.Add(rule);
            }
            if (rules.Count > 0)
                await Log($"Loaded {rules.Count} rule(s) from configuration.");
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
            var rules = GetConfig(ch.Guild.Id) as IEnumerable<ConfigItem>;
            if (rules == null) return;

            foreach (var rule in rules)
            {
                // Checking for mod bypass here (ConfigItem.Match isn't able to access mod list)
                bool isMod = IsModerator(ch.Guild.Id, m);
                await Task.Run(async () => await ProcessMessage(m, rule, isMod));
            }
        }

        /// <summary>
        /// Checks if the incoming message matches the given rule, and executes responses if necessary. 
        /// </summary>
        private async Task ProcessMessage(SocketMessage m, ConfigItem r, bool isMod)
        {
            if (!r.Match(m, isMod)) return;

            // TODO make log optional; configurable
            await Log($"{r} triggered by {m.Author} in {((SocketGuildChannel)m.Channel).Guild.Name}/#{m.Channel.Name}");

            foreach (ResponseBase resp in r.Response)
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
