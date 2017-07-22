using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.RegexResponder
{
    /// <summary>
    /// Implements per-message regex matching and executes customizable responses.
    /// Namesake of this project.
    /// </summary>
    partial class EventProcessor
    {
        private readonly DiscordSocketClient _client;
        private readonly ConfigLoader _conf;

        public EventProcessor(DiscordSocketClient client, ConfigLoader conf)
        {
            _client = client;
            _conf = conf;

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
            if (arg.Author == _client.CurrentUser) return;

            // Looking up server information and extracting settings
            SocketGuild g = ((SocketGuildUser)arg.Author).Guild;
            Server sd = null;
            foreach (var item in _conf.Servers)
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
                        await Logger.GetLogger(ConfigLoader.LogPrefix)
                            ($"Suggestion: Server \"{item.Name}\" can be defined as \"{item.Id}::{item.Name}\"");
                        break;
                    }
                }
            }

            if (sd == null) return; // No server configuration found

            // Further processing is sent to the thread pool
            foreach (var rule in sd.MatchResponseRules)
                await Task.Run(async () => await ProcessMessage(sd, rule, arg));
        }

        /// <summary>
        /// Uses information from a single rule and checks if the incoming message is a match.
        /// If it matches, the rule's responses are executed. To be run in the thread pool.
        /// </summary>
        private async Task ProcessMessage(Server srv, RuleConfig rule, SocketMessage msg)
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
            var log = Logger.GetLogger(rule.DisplayName);
            await log($"Triggered in {srv.Name}/#{msg.Channel} by {msg.Author.ToString()}");

            foreach (string rcmd in rule.Responses)
            {
                string cmd = rcmd.TrimStart(' ').Split(' ')[0].ToLower();
                try
                {
                    ResponseProcessor response;
                    if (!_commands.TryGetValue(cmd, out response))
                    {
                        await log($"Unknown command \"{cmd}\"");
                        continue;
                    }
                    await response.Invoke(log, rcmd, rule, msg);
                }
                catch (Exception ex)
                {
                    await log($"Encountered an error while processing \"{cmd}\"");
                    await log(ex.ToString());
                }
            }
        }

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

            var author = m.Author as SocketGuildUser;
            foreach (var item in ignorelist.Users)
            {
                if (!item.Id.HasValue)
                {
                    // Attempt to update ID if given nick matches
                    if (string.Equals(item.Name, author.Nickname, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.Name, author.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        item.UpdateId(author.Id);
                        return true;
                    }
                } else
                {
                    if (item.Id.Value == author.Id) return true;
                }
            }

            foreach (var item in ignorelist.Roles)
            {
                if (!item.Id.HasValue)
                {
                    // Try to update ID if none exists
                    foreach (var role in author.Roles)
                    {
                        if (string.Equals(item.Name, role.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            item.UpdateId(role.Id);
                            return true;
                        }
                    }
                }
                else
                {
                    if (author.Roles.Any(r => r.Id == item.Id)) return true;
                }
            }

            foreach (var item in ignorelist.Channels)
            {
                if (!item.Id.HasValue)
                {
                    // Try get ID
                    if (string.Equals(item.Name, m.Channel.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        item.UpdateId(m.Channel.Id);
                        return true;
                    }
                }
                else
                {
                    if (item.Id == m.Channel.Id) return true;
                }
            }

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
                        if (string.Equals(ei.Name, ch.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            ei.UpdateId(ch.Id); // Unnecessary, serves only to trigger the suggestion log message
                            return ch;
                        }
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
                        ei.UpdateId(u.Id); // As mentioned above, serves only to trigger the suggestion log
                        return await u.GetOrCreateDMChannelAsync();
                    }
                }
            }

            return null;
        }
    }
}
