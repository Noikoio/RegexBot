using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Feature.AutoRespond
{
    /// <summary>
    /// Similar to <see cref="AutoMod"/>, but lightweight.
    /// Provides the capability to define autoresponses for fun or informational purposes.
    /// <para>
    /// The major differences between this and <see cref="AutoMod"/> include:
    /// <list type="bullet">
    /// <item><description>Does not listen for message edits.</description></item>
    /// <item><description>Moderators are not exempt from any defined triggers.</description></item>
    /// <item><description>Responses are limited to the invoking channel.</description></item>
    /// <item><description>Per-channel rate limiting.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    partial class AutoRespond : BotFeature
    {
        public override string Name => "AutoRespond";

        public AutoRespond(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            // Determine channel type - if not a guild channel, stop.
            var ch = arg.Channel as SocketGuildChannel;
            if (ch == null) return;

            // TODO either search server by name or remove server name support entirely
            var defs = GetConfig(ch.Guild.Id) as IEnumerable<ResponseDefinition>;
            if (defs == null) return;

            foreach (var def in defs)
                await Task.Run(async () => await ProcessMessage(arg, def));
        }

        [ConfigSection("autoresponses")]
        public override async Task<object> ProcessConfiguration(JToken configSection)
        {
            var responses = new List<ResponseDefinition>();
            foreach (var def in configSection.Children<JProperty>())
            {
                // All validation is left to the constructor
                var resp = new ResponseDefinition(def);
                responses.Add(resp);
                await Log($"Added definition '{resp.Label}'");
            }

            return Task.FromResult<object>(responses.AsReadOnly());
        }
    }
}
