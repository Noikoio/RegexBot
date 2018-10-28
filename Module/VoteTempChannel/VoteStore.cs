using System;
using System.Collections.Generic;
using System.Linq;

namespace Noikoio.RegexBot.Module.VoteTempChannel
{
    /// <summary>
    /// Handles keeping track of per-guild voting, along with cooldowns.
    /// </summary>
    class VoteStore
    {
        /*
         * Votes are kept track for a total of five minutes beginning with the first vote.
         * All votes are discarded after five minutes have elapsed, rather than the data
         * staying for longer as more votes are added.
         */
        private Dictionary<ulong, DateTimeOffset> _cooldown;
        private Dictionary<ulong, VoteData> _votes;

        private class VoteData
        {
            public VoteData()
            {
                VotingStart = DateTimeOffset.UtcNow;
                Voters = new List<ulong>();
            }

            public DateTimeOffset VotingStart { get; }
            public List<ulong> Voters { get; }
        }

        public VoteStore()
        {
            _cooldown = new Dictionary<ulong, DateTimeOffset>();
            _votes = new Dictionary<ulong, VoteData>();
        }

        // !! Hardcoded value: votes always expire after 5 minutes.
        static readonly TimeSpan VoteExpiry = new TimeSpan(0, 5, 0);
        /// <summary>
        /// Call before accessing votes. Removes any stale voting entries.
        /// </summary>
        private void CleanVoteData()
        {
            IEnumerable<Tuple<ulong, DateTimeOffset>> expiredEntries;
            lock (_votes)
            {
                var now = DateTimeOffset.UtcNow;
                expiredEntries = (from item in _votes
                                  where now > item.Value.VotingStart + VoteExpiry
                                  select new Tuple<ulong, DateTimeOffset>(item.Key, item.Value.VotingStart))
                                  .ToArray();

                lock (_cooldown)
                {
                    // For expiring votes, set a cooldown that starts at the time the
                    // vote had actually expired.
                    foreach (var item in expiredEntries)
                    {
                        _votes.Remove(item.Item1);
                        _cooldown.Add(item.Item1, item.Item2 + VoteExpiry);
                    }
                }
                
            }
        }

        // !! Hardcoded value: cooldowns last one hour.
        static readonly TimeSpan CooldownExpiry = new TimeSpan(1, 0, 0);
        private bool IsInCooldown(ulong guild)
        {
            lock (_cooldown)
            {
                // Clean up expired entries first...
                var now = DateTimeOffset.UtcNow;
                var expiredEntries = (from item in _cooldown
                                      where now > item.Value + CooldownExpiry
                                      select item.Key).ToArray();
                foreach (var item in expiredEntries) _cooldown.Remove(item);

                // And then the actual check:
                return _cooldown.ContainsKey(guild);
            }
        }

        public void SetCooldown(ulong guild)
        {
            lock (_cooldown) _cooldown.Add(guild, DateTimeOffset.UtcNow);
        }

        public void ClearCooldown(ulong guild)
        {
            lock (_cooldown) _cooldown.Remove(guild);
        }

        /// <summary>
        /// Attempts to log a vote by a given user.
        /// </summary>
        public VoteStatus AddVote(ulong guild, ulong user, out int voteCount)
        {
            voteCount = -1;
            if (IsInCooldown(guild)) return VoteStatus.FailCooldown;
            lock (_votes)
            {
                CleanVoteData();
                VoteData v;
                if (!_votes.TryGetValue(guild, out v))
                {
                    v = new VoteData();
                    _votes[guild] = v;
                }
                voteCount = v.Voters.Count;

                if (v.Voters.Contains(user)) return VoteStatus.FailVotedAlready;

                v.Voters.Add(user);
                voteCount++;
                return VoteStatus.Success;
            }
        }

        public void DelVote(ulong guild, ulong user)
        {
            lock (_votes)
            {
                if (_votes.TryGetValue(guild, out var v))
                {
                    v.Voters.Remove(user);
                    if (v.Voters.Count == 0) _votes.Remove(guild);
                }
            }
        }

        /// <summary>
        /// Clears voting data from within the specified guild.
        /// </summary>
        public void ClearVotes(ulong guild)
        {
            lock (_votes) _votes.Remove(guild);
        }
    }

    enum VoteStatus
    {
        Success, FailVotedAlready, FailCooldown
    }
}
