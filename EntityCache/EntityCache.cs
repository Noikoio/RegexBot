using Discord.WebSocket;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.EntityCache
{
    /// <summary>
    /// Static class for accessing the entity cache.
    /// </summary>
    static class EntityCache
    {
        /*
         * The entity cache works by combining data known/cached by Discord.Net in addition to
         * what has been stored in the database. If data does not exist in the former, it is
         * retrieved from the latter.
         * In either case, the resulting data is placed within a cache item object.
         */

        static DiscordSocketClient _client;
        internal static void SetClient(DiscordSocketClient c) => _client = _client ?? c;

        /// <summary>
        /// Attempts to query for an exact result with the given parameters.
        /// Does not handle exceptions that may occur.
        /// </summary>
        /// <returns>Null on no result.</returns>
        internal static Task<CacheUser> QueryAsync(ulong guild, ulong user)
            => CacheUser.QueryAsync(_client, guild, user);

        /// <summary>
        /// Attempts to look up the user given a search string.
        /// This string looks up case-insensitive, exact matches of nicknames and usernames.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> containing zero or more query results, sorted by cache date.</returns>
        internal static Task<IEnumerable<CacheUser>> QueryAsync(ulong guild, string search)
            => CacheUser.QueryAsync(_client, guild, search);
    }
}
