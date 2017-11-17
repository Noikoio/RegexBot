using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Listens for Discord-based events and writes them to the log (database).
    /// Additionally writes certain messages to a designated logging channel if configured.
    /// </summary>
    class EventListener : BotModule
    {
        public override string Name => "ModLogs";
        public EventListener(DiscordSocketClient client) : base(client)
        {

        }

        [ConfigSection("modlogs")]
        public override Task<object> ProcessConfiguration(JToken configSection)
        {
            throw new NotImplementedException();
        }
    }
}
