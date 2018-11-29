using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.VoiceRoleSync
{
    /// <summary>
    /// Synchronizes a user's state in a voice channel with a role.
    /// In other words: applies a role to a user entering a voice channel. Removes the role when exiting.
    /// </summary>
    class VoiceRoleSync : BotModule
    {
        // Wishlist: specify multiple definitions - multiple channels associated with multiple roles.

        public VoiceRoleSync(DiscordSocketClient client) : base(client)
        {
            client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        }

        private async Task Client_UserVoiceStateUpdated(SocketUser argUser, SocketVoiceState before, SocketVoiceState after)
        {
            // Gather data.
            if (!(argUser is SocketGuildUser user)) return; // not a guild user
            var settings = GetState<GuildSettings>(user.Guild.Id);
            if (settings == null) return; // not enabled here
            var deafened = after.IsDeafened || after.IsSelfDeafened;
            var (settingBefore, settingAfter) = settings.GetChannelSettings(before.VoiceChannel, after.VoiceChannel);

            // Determine action(s) to take
            if (before.VoiceChannel?.Id != after.VoiceChannel?.Id)
            {
                // Joined / Left / Moved voice channels.
                if (settingBefore?.Id != settingAfter?.Id)
                {
                    // Replace roles only if the roles to be applied are different.
                    if (settingBefore != null && user.Roles.Contains(settingBefore)) await user.RemoveRoleAsync(settingBefore);
                    if (settingAfter != null && !user.Roles.Contains(settingAfter)) await user.AddRoleAsync(settingAfter);
                }
            }
            else
            {
                // In same voice channel. Deafen state may have changed.
                if (after.IsDeafened || after.IsSelfDeafened)
                {
                    if (settingAfter != null && user.Roles.Contains(settingAfter)) await user.RemoveRoleAsync(settingAfter);
                }
                else
                {
                    if (settingAfter != null && !user.Roles.Contains(settingAfter)) await user.AddRoleAsync(settingAfter);
                }
            }
        }

        public override Task<object> CreateInstanceState(JToken configSection)
        {
            if (configSection == null) return Task.FromResult<object>(null);
            if (configSection.Type != JTokenType.Object)
            {
                throw new RuleImportException("Expected a JSON object.");
            }
            return Task.FromResult<object>(new GuildSettings((JObject)configSection));
        }

        /// <summary>
        /// Dictionary wrapper. Key = voice channel ID, Value = role.
        /// </summary>
        private class GuildSettings
        {
            private ReadOnlyDictionary<ulong, ulong> _values { get; }

            public GuildSettings(JObject config)
            {
                // Configuration format is expected to be an object that contains other objects.
                // The objects themselves should have their name be the voice channel,
                // and the value be the role to be applied.

                // TODO Make it accept names; currently only accepts ulongs

                var values = new Dictionary<ulong, ulong>();

                foreach (var item in config.Properties())
                {
                    if (!ulong.TryParse(item.Name, out var voice))
                    {
                        throw new RuleImportException($"{item.Name} is not a voice channel ID.");
                    }
                    var valstr = item.Value.Value<string>();
                    if (!ulong.TryParse(valstr, out var role))
                    {
                        throw new RuleImportException($"{valstr} is not a role ID.");
                    }

                    values[voice] = role;
                }

                _values = new ReadOnlyDictionary<ulong, ulong>(values);
            }

            /// <summary>
            /// Gets designated roles for the given two channels (before, after).
            /// Returns null in either for no result/specified role.
            /// </summary>
            public (SocketRole, SocketRole) GetChannelSettings(SocketVoiceChannel before, SocketVoiceChannel after)
                => (GetIndividualResult(before), GetIndividualResult(after));

            private SocketRole GetIndividualResult(SocketVoiceChannel ch)
            {
                if (ch == null) return null;
                if (_values.TryGetValue(ch.Id, out var roleId))
                {
                    return ch.Guild.GetRole(roleId);
                }
                return null;
            }
        }
    }
}
