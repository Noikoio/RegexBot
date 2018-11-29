using Discord;
using Discord.WebSocket;
using System.Text;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.DMLogger
{
    /// <summary>
    /// Listens for and logs direct messages sent to the bot.
    /// The function of this module should be transparent to the user, and thus no configuration is needed.
    /// </summary>
    class DMLogger : BotModule
    {
        public DMLogger(DiscordSocketClient client) : base(client)
        {
            client.MessageReceived += Client_MessageReceived;
            client.MessageUpdated += Client_MessageUpdated;
        }
        
        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (!(arg.Channel is IDMChannel)) return;
            if (arg.Author.IsBot) return;

            await ProcessMessage(arg, false);
        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (!(arg2.Channel is IDMChannel)) return;
            if (arg2.Author.IsBot) return;

            await ProcessMessage(arg2, true);
        }

        private async Task ProcessMessage(SocketMessage arg, bool edited)
        {
            var result = new StringBuilder();
            result.Append(arg.Author.ToString() + (edited ? "(edit) " : "") + ": ");
            if (!string.IsNullOrWhiteSpace(arg.Content))
            {
                if (arg.Content.Contains("\n")) result.AppendLine(); // If multi-line, show sender on separate line
                result.AppendLine(arg.Content);
            }
            foreach (var i in arg.Attachments) result.AppendLine($"[Attachment: {i.Url}]");

            await Log(result.ToString().TrimEnd(new char[] { ' ', '\r', '\n' }));
        }
    }
}
