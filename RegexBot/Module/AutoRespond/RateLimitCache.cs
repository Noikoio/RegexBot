using System;
using System.Collections.Generic;

namespace Noikoio.RegexBot.Module.AutoRespond
{
    /// <summary>
    /// Stores rate limit settings and caches.
    /// </summary>
    class RateLimitCache
    {
        public const ushort DefaultTimeout = 20; // this is Skeeter's fault
        private Dictionary<ulong, DateTime> _cache;

        public uint Timeout { get; }

        /// <summary>
        /// Sets up a new instance of <see cref="RateLimitCache"/>.
        /// </summary>
        public RateLimitCache(uint timeout)
        {
            Timeout = timeout;
            _cache = new Dictionary<ulong, DateTime>();
        }

        /// <summary>
        /// Checks if a "usage" is allowed for the given value.
        /// Items added to cache will be removed after the number of seconds specified in <see cref="Timeout"/>.
        /// </summary>
        /// <param name="id">The ID to add to the cache.</param>
        /// <returns>True on success. False if the given ID already exists.</returns>
        public bool AllowUsage(ulong id)
        {
            if (Timeout == 0) return true;

            lock (this)
            {
                Clean();
                if (_cache.ContainsKey(id)) return false;
                _cache.Add(id, DateTime.Now);
            }
            return true;
        }

        private void Clean()
        {
            var now = DateTime.Now;
            var clean = new Dictionary<ulong, DateTime>();
            foreach (var kp in _cache)
            {
                if (kp.Value.AddSeconds(Timeout) > now)
                {
                    // Copy items that have not yet timed out to the new dictionary
                    clean.Add(kp.Key, kp.Value);
                }
            }
            _cache = clean;
        }
    }
}
