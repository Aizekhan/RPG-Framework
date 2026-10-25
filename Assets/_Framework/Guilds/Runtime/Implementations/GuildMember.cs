using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuildMember.
    /// Represents a guild member with rank, permissions, and contribution tracking.
    /// </summary>
    public class GuildMember : IGuildMember
    {
        private GuildMemberData _data;
        private readonly IGuild _guild;

        public int EntityId { get; private set; }
        public string GuildId => _data.GuildId;
        public GuildRankType Rank => _data.Rank;
        public DateTime JoinedAt => _data.JoinedAt;
        public DateTime LastActiveAt => _data.LastActiveAt;
        public long ContributionPoints => _data.ContributionPoints;
        public string Note => _data.Note;

        /// <summary>
        /// Create a new guild member.
        /// </summary>
        public GuildMember(int entityId, IGuild guild, GuildMemberData data)
        {
            EntityId = entityId;
            _guild = guild;
            _data = data;
        }

        public bool HasPermission(GuildPermission permission)
        {
            var rank = _guild.GetRank(_data.Rank);
            return rank != null && rank.HasPermission(permission);
        }

        public bool HasPrivilege(GuildPrivilege privilege)
        {
            var rank = _guild.GetRank(_data.Rank);
            return rank != null && rank.HasPrivilege(privilege);
        }

        public GuildPermission GetPermissions()
        {
            var rank = _guild.GetRank(_data.Rank);
            return rank?.Permissions ?? GuildPermission.None;
        }

        public GuildPrivilege GetPrivileges()
        {
            var rank = _guild.GetRank(_data.Rank);
            return rank?.Privileges ?? GuildPrivilege.None;
        }

        public void AddContribution(long points)
        {
            if (points > 0)
            {
                _data.ContributionPoints += points;
            }
        }

        public void SetNote(string note)
        {
            _data.Note = note ?? string.Empty;
        }

        public void UpdateLastActive()
        {
            _data.LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Update the member's rank.
        /// Internal method used by GuildManager.
        /// </summary>
        internal void SetRank(GuildRankType newRank)
        {
            _data.Rank = newRank;
        }

        /// <summary>
        /// Get the internal member data.
        /// Internal method for persistence.
        /// </summary>
        internal GuildMemberData GetData()
        {
            return _data;
        }

        public override string ToString()
        {
            return $"GuildMember[{EntityId}]: Rank {Rank}, Contribution {ContributionPoints}";
        }
    }
}
