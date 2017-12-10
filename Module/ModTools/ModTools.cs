using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.ModTools
{
    /// <summary>
    /// ModTools module.
    /// This class manages reading configuration and creating instances based on it.
    /// </summary>
    class ModTools : BotModule
    {
        public override string Name => "ModTools";
        
        public ModTools(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            // Always ignore bots
            if (arg.Author.IsBot) return;

            if (arg.Channel is IDMChannel) await PetitionRelayCheck(arg);
            else if (arg.Channel is IGuildChannel) await CommandCheckInvoke(arg);
        }
        
        [ConfigSection("modtools")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            // Constructor throws exception on config errors
            var conf = new ConfigItem(this, configSection);

            // Log results
            if (conf.Commands.Count > 0)
                await Log(conf.Commands.Count + " command definition(s) loaded.");
            if (conf.PetitionReportingChannel.HasValue)
                await Log("Ban petitioning has been enabled.");

            return conf;
        }

        private new ConfigItem GetConfig(ulong guildId) => (ConfigItem)base.GetConfig(guildId);

        public new Task Log(string text) => base.Log(text);

        private async Task CommandCheckInvoke(SocketMessage arg)
        {
            SocketGuild g = ((SocketGuildUser)arg.Author).Guild;

            // Get guild config
            ServerConfig sc = RegexBot.Config.Servers.FirstOrDefault(s => s.Id == g.Id);
            if (sc == null) return;

            // Disregard if not a bot moderator
            if (!sc.Moderators.ExistsInList(arg)) return;

            // Disregard if the message contains a newline character
            if (arg.Content.Contains("\n")) return;

            // Check for and invoke command
            string cmdchk;
            int spc = arg.Content.IndexOf(' ');
            if (spc != -1) cmdchk = arg.Content.Substring(0, spc);
            else cmdchk = arg.Content;
            if (GetConfig(g.Id).Commands.TryGetValue(cmdchk, out var c))
            {
                try
                {
                    await Log($"'{c.Label}' invoked by {arg.Author.ToString()} in {g.Name}/#{arg.Channel.Name}");
                    await c.Invoke(g, arg);
                }
                catch (Exception ex)
                {
                    await Log($"Encountered an error for the command '{c.Label}'. Details follow:");
                    await Log(ex.ToString());
                }
            }
        }

        #region Ban petitions
        /// <summary>
        /// List of available appeals. Key is user (for quick lookup). Value is guild (for quick config resolution).
        /// TODO expiration?
        /// </summary>
        private Dictionary<ulong, ulong> _openPetitions = new Dictionary<ulong, ulong>();
        public void AddPetition(ulong guild, ulong user)
        {
            // Do nothing if disabled
            if (!GetConfig(guild).PetitionReportingChannel.HasValue) return;
            lock (_openPetitions) _openPetitions[user] = guild;
        }
        private async Task PetitionRelayCheck(SocketMessage msg)
        {
            const string PetitionAccepted = "Your petition has been forwarded to the moderators for review.";
            const string PetitionDenied = "You may not submit a ban petition.";

            // It's possible the sender may still block messages sent to them,
            // hence the empty catch blocks you'll see up ahead.

            if (!msg.Content.StartsWith("!petition ", StringComparison.InvariantCultureIgnoreCase)) return;

            // Input validation
            string ptext = msg.Content.Substring(10);
            if (string.IsNullOrWhiteSpace(ptext))
            {
                // Just ignore.
                return;
            }
            if (ptext.Length > 1000)
            {
                // Enforce petition length limit.
                try { await msg.Author.SendMessageAsync("Your petition message is too long. Try again with a shorter message."); }
                catch (Discord.Net.HttpException) { }
                return;
            }

            ulong targetGuild = 0;
            lock (_openPetitions)
            {
                if (_openPetitions.TryGetValue(msg.Author.Id, out targetGuild))
                {
                    _openPetitions.Remove(msg.Author.Id);
                }
            }
            
            if (targetGuild == 0)
            {
                // Not in the list. Nothing to do.
                try { await msg.Author.SendMessageAsync(PetitionDenied); }
                catch (Discord.Net.HttpException) { }
                return;
            }
            var gObj = Client.GetGuild(targetGuild);
            if (gObj == null)
            {
                // Guild is missing. No longer in guild?
                try { await msg.Author.SendMessageAsync(PetitionDenied); }
                catch (Discord.Net.HttpException) { }
                return;
            }

            // Get petition reporting target
            var pcv = GetConfig(targetGuild).PetitionReportingChannel;
            if (!pcv.HasValue) return; // No target. This should be logically impossible, but... just in case.
            var rch = pcv.Value;
            ISocketMessageChannel rchObj;
            if (!rch.Id.HasValue)
            {
                rchObj = gObj.TextChannels
                    .Where(c => c.Name.Equals(rch.Name, StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();
                // Update value if found
                if (rchObj != null)
                {
                    GetConfig(targetGuild).UpdatePetitionChannel(rchObj.Id);
                }
            }
            else
            {
                rchObj = gObj.GetChannel(rch.Id.Value) as ISocketMessageChannel;
            }

            if (rchObj == null)
            {
                // Channel not found.
                await Log("Petition reporting channel could not be resolved.");
                try { await msg.Author.SendMessageAsync(PetitionDenied); }
                catch (Discord.Net.HttpException) { }
                return;
            }

            // Ready to relay
            try
            {
                await rchObj.SendMessageAsync("", embed: new EmbedBuilder()
                {
                    Color = new Color(0x00FFD9),

                    Author = new EmbedAuthorBuilder()
                    {
                        Name = $"{msg.Author.ToString()} - Ban petition:",
                        IconUrl = msg.Author.GetAvatarUrl()
                    },
                    Description = ptext,
                    Timestamp = msg.Timestamp,

                    Footer = new EmbedFooterBuilder()
                    {
                        Text = "User ID: " + msg.Author.Id
                    }
                });
            }
            catch (Discord.Net.HttpException ex)
            {
                await Log("Failed to relay petition message by " + msg.Author.ToString());
                await Log(ex.Message);
                // For the user's point of view, fail silently.
                try { await msg.Author.SendMessageAsync(PetitionDenied); }
                catch (Discord.Net.HttpException) { }
            }

            // Success. Notify user.
            try { await msg.Author.SendMessageAsync(PetitionAccepted); }
            catch (Discord.Net.HttpException) { }
        }
        #endregion
    }
}
