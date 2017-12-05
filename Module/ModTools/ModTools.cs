using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            if (arg.Channel is IDMChannel) await PetitionRelayCheck(arg);
            else if (arg.Channel is IGuildChannel) await CommandCheckInvoke(arg);
        }

        #region Config
        [ConfigSection("modtools")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            if (configSection.Type != JTokenType.Object)
            {
                throw new RuleImportException("Configuration for this section is invalid.");
            }
            
            // BIG TO DO LIST:
            /*
             * 1. Have commands go into their own space within modtools. Candidate name: "banappeal"
             * 2. Add a property for where to put the petition channel
             * 3. Within ban cmd load, have it check for the existence of a petition channel... if possible?
             *      I guess otherwise silently discard, if the info isn't readily available. I don't know.
             */

            var commands = new Dictionary<string, CommandBase>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in configSection.Children<JProperty>())
            {
                string label = def.Name;
                var cmd = CommandBase.CreateInstance(this, def);
                if (commands.ContainsKey(cmd.Command))
                    throw new RuleImportException(
                        $"{label}: 'command' value must not be equal to that of another definition. " +
                        $"Given value is being used for {commands[cmd.Command].Label}.");

                commands.Add(cmd.Command, cmd);
            }
            await Log($"Loaded {commands.Count} command definition(s).");
            return new ReadOnlyDictionary<string, CommandBase>(commands);
        }

        /*
         * Config is stored in a tuple. I admit, not the best choice...
         * Consider a different approach if more data needs to be stored in the future.
         * Item 1: Command config (Dictionary<string, CommandBase>)
         * Item 2: Ban petition channel (EntityName)
         */

        private new Tuple<Dictionary<string, CommandBase>, EntityName> GetConfig(ulong guildId)
            => (Tuple<Dictionary<string, CommandBase>, EntityName>)base.GetConfig(guildId);
        private Dictionary<string, CommandBase> GetCommandConfig(ulong guild) => GetConfig(guild).Item1;
        private EntityName GetPetitionConfig(ulong guild) => GetConfig(guild).Item2;
        #endregion

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
            if ((GetCommandConfig(g.Id)).TryGetValue(cmdchk, out var c))
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

        /// <summary>
        /// List of available appeals. Key is user (for quick lookup). Value is guild (for quick config resolution).
        /// TODO expiration?
        /// </summary>
        private Dictionary<ulong, ulong> _openPetitions; // Key: user, Value: guild
        public void AddPetition(ulong guild, ulong user)
        {
            lock (_openPetitions) _openPetitions[user] = guild;
        }
        private async Task PetitionRelayCheck(SocketMessage msg)
        {
            const string PetitionAccepted = "Your petition has been forwarded to the moderators for review.";
            const string PetitionDenied = "You may not submit a ban petition.";
            if (!msg.Content.StartsWith("!petition ", StringComparison.InvariantCultureIgnoreCase)) return;

            // Input validation
            string ptext = msg.Content.Substring(10);
            if (string.IsNullOrWhiteSpace(ptext))
            {
                // Just ignore.
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

            // It's possible the sender may still block messages sent to them,
            // hence the empty catch blocks you'll see up ahead.

            if (targetGuild == 0)
            {
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

            // Get petition reporting target if not already known
            var rch = GetPetitionConfig(targetGuild);
            ISocketMessageChannel rchObj;
            if (!rch.Id.HasValue)
            {
                rchObj = gObj.TextChannels
                    .Where(c => c.Name.Equals(rch.Name, StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();
            }
            else
            {
                rchObj = (ISocketMessageChannel)gObj.GetChannel(rch.Id.Value);
            }
            if (rchObj == null)
            {
                // Channel not found.
                await Log("Petition reporting channel could not be resolved.");
                try { await msg.Author.SendMessageAsync(PetitionDenied); }
                catch (Discord.Net.HttpException) { }
                return;
            }

            // Ready to relay as embed
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
                    Timestamp = msg.Timestamp
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
    }
}
