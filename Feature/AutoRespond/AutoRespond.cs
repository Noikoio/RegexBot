using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace Noikoio.RegexBot.Feature.AutoRespond
{
    /// <summary>
    /// Similar to <see cref="AutoMod"/>, but lightweight.
    /// Provides the capability to define autoresponses for fun or informational purposes.
    /// <para>
    /// The major differences between this and <see cref="AutoMod"/> include:
    /// <list type="bullet">
    /// <item><description>Does not listen for message edits.</description></item>
    /// <item><description>Moderators are not exempt from any defined triggers by default.</description></item>
    /// <item><description>Responses are limited to only two types, and only one is allowed per rule.</description></item>
    /// <item><description>Does not support fine-grained matching options.</description></item>
    /// <item><description>Support for rate limiting.</description></item>
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
        public override Task<object> ProcessConfiguration(JToken configSection)
        {
            throw new NotImplementedException();
        }
    }
}
