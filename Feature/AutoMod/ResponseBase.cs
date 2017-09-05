using Discord;
using Discord.WebSocket;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoMod
{
    /// <summary>
    /// Base class for all Response classes.
    /// Contains helper methods for use by response code.
    /// </summary>
    [DebuggerDisplay("Response: {_cmdline}")]
    abstract class ResponseBase
    {
        private readonly ConfigItem _rule;
        private readonly string _cmdline;
        
        protected ConfigItem Rule => _rule;
        private DiscordSocketClient Client => _rule.Discord;
        public string CmdLine => _cmdline;
        public string CmdArg0 {
            get {
                int i = _cmdline.IndexOf(' ');
                if (i != -1) return _cmdline.Substring(0, i);
                return _cmdline;
            }
        }

        /// <summary>
        /// Deriving constructor should do validation of incoming <paramref name="cmdline"/>.
        /// </summary>
        public ResponseBase(ConfigItem rule, string cmdline)
        {
            _rule = rule;
            _cmdline = cmdline;
        }

        public abstract Task Invoke(SocketMessage msg);

        protected async Task Log(string text)
        {
            int dl = _cmdline.IndexOf(' ');
            var prefix = _cmdline.Substring(0, dl);
            await Rule.Logger(prefix + ": " + text);
        }

        #region Config loading
        private static readonly ReadOnlyDictionary<string, Type> _commands =
            new ReadOnlyDictionary<string, Type>(
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                // Define all accepted commands and their corresponding types here
                { "ban",            typeof(Responses.Ban) },
                { "kick",           typeof(Responses.Kick) },
                { "say",            typeof(Responses.Say) },
                { "send",           typeof(Responses.Say) },
                { "delete",         typeof(Responses.Remove) },
                { "remove",         typeof(Responses.Remove) },
                { "report",         typeof(Responses.Report) },
                { "addrole",        typeof(Responses.RoleManipulation) },
                { "grantrole",      typeof(Responses.RoleManipulation) },
                { "delrole",        typeof(Responses.RoleManipulation) },
                { "removerole",     typeof(Responses.RoleManipulation) },
                { "revokerole",     typeof(Responses.RoleManipulation) }
            });

        public static ResponseBase[] ReadConfiguration(ConfigItem r, IEnumerable<string> responses)
        {
            var result = new List<ResponseBase>();
            foreach (var line in responses)
            {
                if (string.IsNullOrWhiteSpace(line))
                    throw new RuleImportException("Empty response line");
                int i = line.IndexOf(' ');
                string basecmd;
                if (i != -1) basecmd = line.Substring(0, i);
                else basecmd = line;

                Type rt;
                if (!_commands.TryGetValue(basecmd, out rt))
                    throw new RuleImportException($"'{basecmd}' is not a valid response");

                var newresponse = Activator.CreateInstance(rt, r, line) as ResponseBase;
                if (newresponse == null)
                    throw new Exception("An unknown error occurred when attempting to create a new Response object.");
                result.Add(newresponse);
            }
            return result.ToArray();
        }
        #endregion

        #region Helper methods
        /// <summary>
        /// Receives a string (beginning with @ or #) and returns an object
        /// suitable for sending out messages
        /// </summary>
        protected async Task<IMessageChannel> GetMessageTargetAsync(string targetName, SocketMessage m)
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
                    return await Client.GetUser(ei.Id.Value).GetOrCreateDMChannelAsync();
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

        protected string ProcessText(string input, SocketMessage m)
        {
            // Maybe in the future this will do more.
            // For now, replaces all instances of @_ with the message sender.
            return input
                .Replace("@_", m.Author.Mention)
                .Replace("@\\_", "@_");
        }
        #endregion
    }
}
