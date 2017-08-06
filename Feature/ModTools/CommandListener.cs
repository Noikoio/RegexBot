﻿using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.ModTools
{
    /// <summary>
    /// Entry point for the ModTools feature.
    /// This feature implements moderation commands that are defined and enabled in configuration.
    /// </summary>
    // We are not using Discord.Net's Commands extension, as it doesn't allow for changes during runtime.
    class CommandListener : BotFeature
    {
        public override string Name => "ModTools";
        
        public CommandListener(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            // Disregard if not in a guild
            SocketGuild g = (arg.Author as SocketGuildUser)?.Guild;
            if (g == null) return;

            // Get guild config
            ServerConfig sc = RegexBot.Config.Servers.FirstOrDefault(s => s.Id == g.Id);
            if (sc == null) return;

            // Disregard if not a bot moderator
            // TODO have this and RegexResponder call the same relevant code
            if (!IsInList(sc.Moderators, arg)) return;

            // Disregard if the message contains a newline character
            if (arg.Content.Contains("\n")) return;

            // Check for and invoke command...
            string cmdchk;
            int spc = arg.Content.IndexOf(' ');
            if (spc != -1) cmdchk = arg.Content.Substring(0, spc);
            else cmdchk = arg.Content;
            if (((IDictionary<string, CommandBase>)GetConfig(g.Id)).TryGetValue(cmdchk, out var c))
            {
                // ...on the thread pool.
                await Task.Run(async () =>
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
                });
            }
        }

        [ConfigSection("modtools")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            var newcmds = new Dictionary<string, CommandBase>(StringComparer.OrdinalIgnoreCase);
            foreach (JObject definition in configSection)
            {
                string label = definition["label"].Value<string>();
                if (string.IsNullOrWhiteSpace(label))
                    throw new RuleImportException("A 'label' value was not specified in a command definition.");

                string cmdinvoke = definition["command"].Value<string>();
                if (string.IsNullOrWhiteSpace(cmdinvoke))
                    throw new RuleImportException($"{label}: 'command' value was not specified.");
                if (cmdinvoke.Contains(" "))
                    throw new RuleImportException($"{label}: 'command' must not contain spaces.");
                if (newcmds.TryGetValue(cmdinvoke, out var cmdexisting))
                    throw new RuleImportException(
                        $"{label}: 'command' value must not be equal to that of another definition. " +
                        $"Given value is being used for {cmdexisting.Label}.");
                        

                string ctypestr = definition["type"].Value<string>();
                if (string.IsNullOrWhiteSpace(ctypestr))
                    throw new RuleImportException($"Value 'type' must be specified in definition for '{label}'.");
                var ctype = CommandTypeAttribute.GetCommandType(ctypestr);

                CommandBase cmd;
                try
                {
                    cmd = (CommandBase)Activator.CreateInstance(ctype, this, definition);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is RuleImportException)
                        throw new RuleImportException($"Error in configuration for '{label}': {ex.InnerException.Message}");
                    throw;
                }
                await Log($"'{label}' created; using command {cmdinvoke}");
                newcmds.Add(cmdinvoke, cmd);
            }
            return new ReadOnlyDictionary<string, CommandBase>(newcmds);
        }

        public new Task Log(string text) => base.Log(text);

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
                }
                else
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
    }
}
