using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Noikoio.RegexBot.Feature.RegexResponder
{
    /// <summary>
    /// Implements per-message regex matching and executes customizable responses.
    /// Namesake of this project.
    /// </summary>
    partial class EventProcessor : BotFeature
    {
        private readonly DiscordSocketClient _client;

        public override string Name => "RegexResponder";

        public EventProcessor(DiscordSocketClient client) : base(client)
        {
            _client = client;

            _client.MessageReceived += OnMessageReceived;
            _client.MessageUpdated += OnMessageUpdated;

            _commands = new ReadOnlyDictionary<string, ResponseProcessor>(
                new Dictionary<string, ResponseProcessor>() {
#if DEBUG
                    { "crash", RP_Crash },
                    { "dumpid", RP_DumpID },
#endif
                    { "report", RP_Report },
                    { "say", RP_Say },
                    { "remove", RP_Remove },
                    { "delete", RP_Remove },
                    { "erase", RP_Remove },
                    { "exec", RP_Exec },
                    { "ban", RP_Ban },
                    { "grantrole", RP_GrantRevokeRole },
                    { "revokerole", RP_GrantRevokeRole }
                }
            );
        }

        #region Event handlers
        private async Task OnMessageReceived(SocketMessage arg)
            => await ReceiveMessage(arg);
        private async Task OnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
            => await ReceiveMessage(arg2);
        #endregion

        /// <summary>
        /// Receives incoming messages and creates tasks to handle them if necessary.
        /// </summary>
        private async Task ReceiveMessage(SocketMessage arg)
        {
            // Determine channel type - if not a guild channel, stop.
            var ch = arg.Channel as SocketGuildChannel;
            if (ch == null) return;

            if (arg.Author == _client.CurrentUser) return; // Don't ever self-trigger

            // Looking up server information and extracting settings
            SocketGuild g = ch.Guild;
            ServerConfig sd = null;
            foreach (var item in RegexBot.Config.Servers)
            {
                if (item.Id.HasValue)
                {
                    // Finding server by ID
                    if (g.Id == item.Id)
                    {
                        sd = item;
                        break;
                    }
                }
                else
                {
                    // Finding server by name and caching ID
                    if (string.Equals(item.Name, g.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Id = g.Id;
                        sd = item;
                        await Logger.GetLogger(Configuration.LogPrefix)
                            ($"Suggestion: Server \"{item.Name}\" can be defined as \"{item.Id}::{item.Name}\"");
                        break;
                    }
                }
            }

            if (sd == null) return; // No server configuration found
            var rules = GetConfig(ch.Guild.Id) as IEnumerable<RuleConfig>;
            if (rules == null) return;

            // Further processing is sent to the thread pool
            foreach (var rule in rules)
                await Task.Run(async () => await ProcessMessage(sd, rule, arg));
        }

        /// <summary>
        /// Uses information from a single rule and checks if the incoming message is a match.
        /// If it matches, the rule's responses are executed. To be run in the thread pool.
        /// </summary>
        private async Task ProcessMessage(ServerConfig srv, RuleConfig rule, SocketMessage msg)
        {
            string msgcontent;

            // Embed mode?
            if (rule.MatchEmbeds)
            {
                var embeds = new StringBuilder();
                foreach (var e in msg.Embeds) embeds.AppendLine(EmbedToString(e));
                msgcontent = embeds.ToString();
            }
            else
            {
                msgcontent = msg.Content;
            }

            // Min/max message length check
            if (rule.MinLength.HasValue && msgcontent.Length <= rule.MinLength.Value) return;
            if (rule.MaxLength.HasValue && msgcontent.Length >= rule.MaxLength.Value) return;

            // Moderator bypass check
            if (rule.AllowModBypass == true && IsInList(srv.Moderators, msg)) return;
            // Individual rule filtering check
            if (IsFiltered(rule, msg)) return;

            // And finally, pattern matching checks
            bool success = false;
            foreach (var regex in rule.Regex)
            {
                success = regex.Match(msgcontent).Success;
                if (success) break;
            }
            if (!success) return;

            // Prepare to execute responses
            await Log($"\"{rule.DisplayName}\" triggered in {srv.Name}/#{msg.Channel} by {msg.Author.ToString()}");

            foreach (string rcmd in rule.Responses)
            {
                string cmd = rcmd.TrimStart(' ').Split(' ')[0].ToLower();
                try
                {
                    ResponseProcessor response;
                    if (!_commands.TryGetValue(cmd, out response))
                    {
                        await Log($"Unknown command defined in response: \"{cmd}\"");
                        continue;
                    }
                    await response.Invoke(rcmd, rule, msg);
                }
                catch (Exception ex)
                {
                    await Log($"Encountered an error while processing \"{cmd}\". Details follow:");
                    await Log(ex.ToString());
                }
            }
        }

        [ConfigSection("rules")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            List<RuleConfig> rules = new List<RuleConfig>();
            foreach (JObject ruleconf in configSection)
            {
                // Try and get at least the name before passing it to RuleItem
                string name = ruleconf["name"]?.Value<string>();
                if (name == null)
                {
                    await Log("Display name not defined within a rule section.");
                    return false;
                }
                await Log($"Adding rule \"{name}\"");

                RuleConfig rule;
                try
                {
                    rule = new RuleConfig(ruleconf);
                }
                catch (RuleImportException ex)
                {
                    await Log("-> Error: " + ex.Message);
                    return false;
                }
                rules.Add(rule);
            }

            return rules.AsReadOnly();
        }

        // -------------------------------------

        /// <summary>
        /// Turns an embed into a single string for regex matching purposes
        /// </summary>
        private string EmbedToString(Embed e)
        {
            StringBuilder result = new StringBuilder();
            if (e.Author.HasValue) result.AppendLine(e.Author.Value.Name ?? "" + e.Author.Value.Url ?? "");

            if (!string.IsNullOrWhiteSpace(e.Title)) result.AppendLine(e.Title);
            if (!string.IsNullOrWhiteSpace(e.Description)) result.AppendLine(e.Description);

            foreach (var f in e.Fields)
            {
                if (!string.IsNullOrWhiteSpace(f.Name)) result.AppendLine(f.Name);
                if (!string.IsNullOrWhiteSpace(f.Value)) result.AppendLine(f.Value);
            }

            if (e.Footer.HasValue)
            {
                result.AppendLine(e.Footer.Value.Text ?? "");
            }

            return result.ToString();
        }
        
        private bool IsFiltered(RuleConfig r, SocketMessage m)
        {
            if (r.FilterMode == FilterType.None) return false;

            bool inFilter = IsInList(r.FilterList, m);
            
            if (r.FilterMode == FilterType.Whitelist)
            {
                if (!inFilter) return true;
                return IsInList(r.FilterExemptions, m);
            }
            else if (r.FilterMode == FilterType.Blacklist)
            {
                if (!inFilter) return false;
                return !IsInList(r.FilterExemptions, m);
            }

            return false; // this shouldn't happen™
        }

        private bool IsInList(EntityList ignorelist, SocketMessage m)
        {
            if (ignorelist == null)
            {
                // This happens when getting a message from a server not defined in config.
                return false;
            }

            var guildauthor = m.Author as SocketGuildUser;
            foreach (var item in ignorelist.Users)
            {
                if (!item.Id.HasValue)
                {
                    if (guildauthor != null &&
                        string.Equals(item.Name, guildauthor.Nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    
                    if (string.Equals(item.Name, m.Author.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                } else
                {
                    if (item.Id.Value == m.Author.Id) return true;
                }
            }

            if (guildauthor != null)
            {
                foreach (var guildrole in guildauthor.Roles)
                {
                    if (ignorelist.Roles.Any(listrole =>
                    {
                        if (listrole.Id.HasValue) return listrole.Id == guildrole.Id;
                        else return string.Equals(listrole.Name, guildrole.Name, StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        return true;
                    }
                }

                foreach (var listchannel in ignorelist.Channels)
                {
                    if (listchannel.Id.HasValue && listchannel.Id == m.Channel.Id ||
                        string.Equals(listchannel.Name, m.Channel.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // No match.
            return false;
        }

        private string[] SplitParams(string cmd, int? limit = null)
        {
            if (limit.HasValue)
            {
                return cmd.Split(new char[] { ' ' }, limit.Value, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                return cmd.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private string ProcessText(string input, SocketMessage m)
        {
            // Maybe in the future this will do more.
            // For now, replaces all instances of @_ with the message sender.
            return input
                .Replace("@_", m.Author.Mention)
                .Replace("@\\_", "@_");
        }

        /// <summary>
        /// Receives a string (beginning with @ or #) and returns an object
        /// suitable for sending out messages
        /// </summary>
        private async Task<IMessageChannel> GetMessageTargetAsync(string targetName, SocketMessage m)
        {
            const string AEShort = "Target name is too short.";

            EntityType et;
            if (targetName.Length <= 1) throw new ArgumentException(AEShort);

            if (targetName[0] == '#') et = EntityType.Channel;
            else if (targetName[0] == '@') et = EntityType.User;
            else throw new ArgumentException("Target is not specified to be either a channel or user.");

            targetName = targetName.Substring(1);
            if (targetName == "_")
            {
                if (et == EntityType.Channel) return m.Channel;
                else return await m.Author.GetOrCreateDMChannelAsync();
            }

            EntityName ei = new EntityName(targetName, et);
            SocketGuild g = ((SocketGuildUser)m.Author).Guild;

            if (et == EntityType.Channel)
            {
                if (targetName.Length < 2 || targetName.Length > 100)
                    throw new ArgumentException(AEShort);
                
                foreach (var ch in g.TextChannels)
                {
                    if (ei.Id.HasValue)
                    {
                        if (ei.Id.Value == ch.Id) return ch;
                    }
                    else
                    {
                        if (string.Equals(ei.Name, ch.Name, StringComparison.OrdinalIgnoreCase)) return ch;
                    }
                }
            }
            else
            {
                if (ei.Id.HasValue)
                {
                    // The easy way
                    return await _client.GetUser(ei.Id.Value).GetOrCreateDMChannelAsync();
                }

                // The hard way
                foreach (var u in g.Users)
                {
                    if (string.Equals(ei.Name, u.Username, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ei.Name, u.Nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        return await u.GetOrCreateDMChannelAsync();
                    }
                }
            }

            return null;
        }
    }
}
