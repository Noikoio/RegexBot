using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    class Configuration
    {
        /// <summary>
        /// Channel name in which voting takes place.
        /// </summary>
        public string VoteChannel { get; }
        /// <summary>
        /// Command used to vote for the channel's creation.
        /// </summary>
        public string VoteCommand { get; }
        /// <summary>
        /// Number of votes needed to create the channel.
        /// </summary>
        public int VotePassThreshold { get; }
        /// <summary>
        /// Amount of time that a voting session can last starting from its initial vote.
        /// </summary>
        public TimeSpan VotingDuration { get; }
        /// <summary>
        /// Amount of time to wait before another vote may be initiated, either after a failed vote
        /// or from expiration of the temporary channel.
        /// </summary>
        public TimeSpan VotingCooldown { get; }

        /// <summary>
        /// Name of the temporary channel, without prefix.
        /// </summary>
        public string TempChannelName { get; }

        /// <summary>
        /// Amount of time that the temporary channel can exist without activity before expiring and being deleted.
        /// </summary>
        public TimeSpan ChannelDuration { get; }

        public Configuration(JObject j)
        {
            VoteCommand = j["VoteCommand"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(VoteCommand))
                throw new RuleImportException("'VoteCommand' must be specified.");
            if (VoteCommand.Contains(" "))
                throw new RuleImportException("'VoteCommand' must not contain spaces.");

            TempChannelName = ParseChannelNameConfig(j, "TempChannelName");
            VoteChannel = ParseChannelNameConfig(j, "VoteChannel");

            var vptProp = j["VotePassThreshold"];
            if (vptProp == null)
                throw new RuleImportException("'VotePassThreshold' must be specified.");
            if (vptProp.Type != JTokenType.Integer)
                throw new NotImplementedException("'VotePassThreshold' must be an integer.");
            VotePassThreshold = vptProp.Value<int>();
            if (VotePassThreshold <= 0)
                throw new NotImplementedException("'VotePassThreshold' must be greater than zero.");

            ChannelDuration = ParseTimeConfig(j, "ChannelDuration");
            VotingDuration = ParseTimeConfig(j, "VotingDuration");
            VotingCooldown = ParseTimeConfig(j, "VotingCooldown");
        }

        private string ParseChannelNameConfig(JObject conf, string valueName)
        {
            var value = conf[valueName]?.Value<string>();
            if (string.IsNullOrWhiteSpace(value))
                throw new RuleImportException($"'{valueName}' must be specified.");
            if (!Regex.IsMatch(value, @"^([A-Za-z0-9]|[-_ ])+$"))
                throw new RuleImportException($"'{valueName}' contains one or more invalid characters.");
            return value;
        }

        private TimeSpan ParseTimeConfig(JObject conf, string valueName)
        {
            var inputstr = conf[valueName]?.Value<string>();
            if (string.IsNullOrWhiteSpace(inputstr))
                throw new RuleImportException($"'{valueName}' must be specified.");

            try
            {
                return ParseShorthandTimeInput(inputstr);
            }
            catch (ArgumentException)
            {
                throw new RuleImportException($"'{valueName}' could not be parsed as a length of time. See documentation.");
            }
        }

        private static readonly Regex ShorthandTimeInput = new Regex(@"^(?:(?<day>\d+)d)?(?:(?<hr>\d+)h)?(?:(?<min>\d+)m)?$");
        // TODO Could be improved or better adapted? I copied this straight from an old project.
        public static TimeSpan ParseShorthandTimeInput(string ti)
        {
            ti = ti.ToLower();
            var time = ShorthandTimeInput.Match(ti);
            if (!time.Success) throw new ArgumentException("Input is not shorthand time.");

            int minutes = 0;
            string inday = time.Groups["day"].Value;
            string inhr = time.Groups["hr"].Value;
            string inmin = time.Groups["min"].Value;
            if (inday != "")
            {
                minutes += int.Parse(inday) * 1440;
            }
            if (inhr != "")
            {
                minutes += int.Parse(inhr) * 60;
            }
            if (inmin != "")
            {
                minutes += int.Parse(inmin);
            }
            return new TimeSpan(0, minutes, 0);
        }
    }
}
