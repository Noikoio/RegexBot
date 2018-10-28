using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    class GuildConfiguration
    {
        public string VoteCommand { get; }
        public string TempChannelName { get; }
        public TimeSpan ChannelBaseDuration { get; }
        public TimeSpan ChannelExtendDuration { get; }
        public TimeSpan KeepaliveVoteDuration { get; }
        public int VotePassThreshold { get; }

        public GuildConfiguration(JObject j)
        {
            VoteCommand = j["VoteCommand"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(VoteCommand))
                throw new RuleImportException("'VoteCommand' must be specified.");
            if (VoteCommand.Contains(" "))
                throw new RuleImportException("'VoteCommand' must not contain spaces.");

            TempChannelName = j["TempChannelName"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(TempChannelName))
                throw new RuleImportException("'TempChannelName' must be specified.");
            if (!Regex.IsMatch(TempChannelName, @"^([A-Za-z0-9]|[-_ ])+$"))
                throw new RuleImportException("'TempChannelName' contains one or more invalid characters.");

            var vptProp = j["VotePassThreshold"];
            if (vptProp == null)
                throw new RuleImportException("'VotePassThreshold' must be specified.");
            if (vptProp.Type != JTokenType.Integer)
                throw new NotImplementedException("'VotePassThreshold' must be an integer.");
            VotePassThreshold = vptProp.Value<int>();
            if (VotePassThreshold <= 0)
                throw new NotImplementedException("'VotePassThreshold' must be greater than zero.");

            ChannelBaseDuration = ParseTimeConfig(j, "ChannelBaseDuration");
            ChannelExtendDuration = ParseTimeConfig(j, "ChannelExtendDuration");
            KeepaliveVoteDuration = ParseTimeConfig(j, "KeepaliveVoteDuration");
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
