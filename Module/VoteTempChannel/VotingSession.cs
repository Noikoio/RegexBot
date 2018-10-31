using System;
using System.Collections.Generic;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Keeps information on voting sessions and cooldowns.
    /// </summary>
    class VotingSession
    {
        private Configuration _conf;
        private DateTimeOffset _initialVoteTime;
        private List<ulong> _votes;
        public DateTimeOffset? _cooldownStart;

        public VotingSession(Configuration conf)
        {
            _conf = conf;
            _cooldownStart = null;
            _votes = new List<ulong>();
        }

        /// <summary>
        /// Counts a user vote.
        /// </summary>
        /// <returns>False if the user already has a vote counted.</returns>
        public bool AddVote(ulong id, out int voteCount)
        {
            // Mark the start of a new session, if applicable.
            if (_votes.Count == 0) _initialVoteTime = DateTimeOffset.UtcNow;
            if (_votes.Contains(id))
            {
                voteCount = -1;
                return false;
            }
            _votes.Add(id);
            voteCount = _votes.Count;
            return true;
        }

        /// <summary>
        /// Checks if the voting session has expired.
        /// To be called by the background task. This automatically resets and sets cooldown.
        /// </summary>
        public bool IsSessionExpired()
        {
            if (_votes.Count == 0) return false;
            if (DateTimeOffset.UtcNow > _initialVoteTime + _conf.VotingDuration)
            {
                // Clear votes. And because we're clearing it due to an expiration, set a cooldown.
                Reset();
                StartCooldown();
                return true;
            }
            return false;
        }

        public void StartCooldown()
        {
            _cooldownStart = DateTimeOffset.UtcNow;
        }

        public bool IsInCooldown()
        {
            if (!_cooldownStart.HasValue) return false;
            return _cooldownStart.Value + _conf.VotingCooldown > DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Resets the object to its initial state.
        /// </summary>
        public void Reset()
        {
            _votes.Clear();
            _cooldownStart = null;
        }
    }
}
