using System;
using System.Collections.Generic;
using System.Text;

namespace Noikoio.RegexBot.Module.ModLogs
{
    /// <summary>
    /// Represents a log entry.
    /// </summary>
    class Entry
    {
        readonly int _logId;
        readonly DateTime _ts;
        readonly ulong? _invokeId;
        readonly ulong _targetId;
        readonly ulong? _channelId;
        readonly string _type;
        readonly string _message;

        /// <summary>
        /// Gets the ID value of this log entry.
        /// </summary>
        public int Id => _logId;
        /// <summary>
        /// Gets the timestamp (a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>) of the entry.
        /// </summary>
        public DateTime Timestamp => _ts;
        /// <summary>
        /// Gets the ID of the invoking user.
        /// This value exists only if this entry was created through action of another user that is not the target.
        /// </summary>
        public ulong? Invoker => _invokeId;
        /// <summary>
        /// Gets the ID of the user to which this log entry corresponds.
        /// </summary>
        public ulong Target => _targetId;
        /// <summary>
        /// Gets the guild channel ID to which this log entry corresponds, if any.
        /// </summary>
        public ulong? TargetChannel => _channelId;
        /// <summary>
        /// Gets this log entry's 'type', or category.
        /// </summary>
        public string LogType => _type;
        /// <summary>
        /// Gets the content of this log entry.
        /// </summary>
        public string Message => _message;

        public Entry()
        {
            throw new NotImplementedException();
        }

        // TODO figure out some helper methods to retrieve data of other entities by ID, if it becomes necessary
    }
}
