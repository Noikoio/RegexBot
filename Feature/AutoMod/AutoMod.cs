using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace Noikoio.RegexBot.Feature.AutoMod
{
    /// <summary>
    /// Implements per-message regex matching and executes customizable responses.
    /// The name RegexBot comes from the existence of this feature.
    /// 
    /// Strictly for use as a moderation tool only. Triggers that respond only to messages
    /// should be configured using <see cref="AutoRespond"/>.
    /// </summary>
    class AutoMod : BotFeature
    {
        public override string Name => "AutoMod";

        public AutoMod(DiscordSocketClient client) : base(client)
        {
            throw new NotImplementedException();
        }

        public override Task<object> ProcessConfiguration(JToken configSection)
        {
            throw new NotImplementedException();
        }
    }
}
