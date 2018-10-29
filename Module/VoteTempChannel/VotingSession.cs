using System;
using System.Collections.Generic;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Keeps information on voting sessions and voting cooldowns.
    /// </summary>
    class VotingSession
    {
        private Configuration _conf;
        private DateTimeOffset _initialVoteTime;
        private List<ulong> _votes;

        public DateTimeOffset? CooldownStart { get; private set; }

        public VotingSession()
        {
            CooldownStart = null;
            _votes = new List<ulong>();
        }

        /// <summary>
        /// Counts a user vote.
        /// </summary>
        /// <returns>False if the user already has a vote counted.</returns>
        public bool AddVote(ulong id)
        {
            // Mark the start of a new session, if applicable.
            if (_votes.Count == 0) _initialVoteTime = DateTimeOffset.UtcNow;
            if (_votes.Contains(id)) return false;
            _votes.Add(id);
            return true;
        }

        /// <summary>
        /// Checks if the voting session has expired.
        /// To be called by the background task. This automatically resets state.
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
            CooldownStart = DateTimeOffset.UtcNow;
        }

        public bool IsInCooldown()
        {
            if (!CooldownStart.HasValue) return false;
            return CooldownStart.Value + _conf.VotingCooldown > DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Resets the object to its initial state.
        /// </summary>
        public void Reset()
        {
            _votes.Clear();
            CooldownStart = null;
        }
    }
}
