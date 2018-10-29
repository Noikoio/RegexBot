using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Guild state object. Contains known information about the guild.
    /// Contains helper functions that may involve usage of data contained within.
    /// </summary>
    class GuildInformation
    {
        public Configuration Config { get; }
        public VotingSession Voting { get; }

        /// <summary>
        /// Timestamp of last activity in the temporary channel.
        /// Used to determine its expiration.
        /// </summary>
        public DateTimeOffset TempChannelLastActivity { get; set; }

        public GuildInformation(JObject conf)
        {
            // In case temp channel exists as we (re)start, begin a new timer for it.
            TempChannelLastActivity = DateTimeOffset.UtcNow;

            Config = new Configuration(conf);
            Voting = new VotingSession();
        }

        public SocketTextChannel GetTemporaryChannel(SocketGuild guild)
        {
            foreach (var ch in guild.TextChannels)
            {
                if (string.Equals(ch.Name, Config.TempChannelName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return ch;
                }
            }
            return null;
        }

        public bool IsTempChannelExpired()
        {
            return DateTimeOffset.UtcNow > TempChannelLastActivity + Config.ChannelDuration;
        }
    }
}
