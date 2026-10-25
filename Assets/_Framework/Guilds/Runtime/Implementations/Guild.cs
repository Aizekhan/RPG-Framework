using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Factions;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuild.
    /// Extends Faction to add guild-specific features like membership and ranks.
    /// </summary>
    public class Guild : Faction, IGuild
    {
        private readonly GuildData _data;
        private readonly Dictionary<int, IGuildMember> _members;
        private readonly Dictionary<GuildRankType, IGuildRank> _ranks;

        public string GuildId => _data.GuildId;
        public string MessageOfTheDay => _data.MessageOfTheDay;
        public string Tag => _data.Tag;
        public DateTime CreatedAt => _data.CreatedAt;
        public int LeaderId => _data.LeaderId;
        public int MemberCount => _members.Count;
        public int MaxMembers => _data.MaxMembers;
        public int Level => _data.Level;
        public long Experience => _data.Experience;

        /// <summary>
        /// Create a new guild.
        /// </summary>
        public Guild(GuildData data) : base(
            data.GuildId,
            data.Name,
            data.Description,
            FactionType.Guild,
            defaultReputation: 0,
            isActive: data.IsActive)
        {
            _data = data;
            _members = new Dictionary<int, IGuildMember>();
            _ranks = InitializeDefaultRanks();
        }

        /// <summary>
        /// Initialize default rank structure.
        /// </summary>
        private Dictionary<GuildRankType, IGuildRank> InitializeDefaultRanks()
        {
            return new Dictionary<GuildRankType, IGuildRank>
            {
                { GuildRankType.Initiate, new GuildRank(
                    GuildRankType.Initiate,
                    "Initiate",
                    0,
                    GuildPermission.DepositTreasury | GuildPermission.ViewTreasury,
                    GuildPrivilege.UseGuildChat | GuildPrivilege.UseGuildHall | GuildPrivilege.QuestRewards
                )},
                { GuildRankType.Member, new GuildRank(
                    GuildRankType.Member,
                    "Member",
                    1,
                    GuildPermission.DepositTreasury | GuildPermission.ViewTreasury | GuildPermission.Invite,
                    GuildPrivilege.UseGuildChat | GuildPrivilege.UseGuildHall | GuildPrivilege.QuestRewards |
                    GuildPrivilege.AccessBank | GuildPrivilege.ParticipateInRaids | GuildPrivilege.ParticipateInPvP
                )},
                { GuildRankType.Officer, new GuildRank(
                    GuildRankType.Officer,
                    "Officer",
                    2,
                    GuildPermission.DepositTreasury | GuildPermission.ViewTreasury | GuildPermission.Invite |
                    GuildPermission.Kick | GuildPermission.Promote | GuildPermission.Demote |
                    GuildPermission.EditMotd | GuildPermission.SetMemberNotes | GuildPermission.OrganizeEvents,
                    GuildPrivilege.UseGuildChat | GuildPrivilege.UseOfficerChat | GuildPrivilege.UseGuildHall |
                    GuildPrivilege.QuestRewards | GuildPrivilege.AccessBank | GuildPrivilege.ParticipateInRaids |
                    GuildPrivilege.ParticipateInPvP | GuildPrivilege.RepairDiscount | GuildPrivilege.EventPriority
                )},
                { GuildRankType.Elder, new GuildRank(
                    GuildRankType.Elder,
                    "Elder",
                    3,
                    GuildPermission.DepositTreasury | GuildPermission.ViewTreasury | GuildPermission.Invite |
                    GuildPermission.Kick | GuildPermission.Promote | GuildPermission.Demote |
                    GuildPermission.WithdrawGold | GuildPermission.WithdrawItems | GuildPermission.EditMotd |
                    GuildPermission.ManageAlliances | GuildPermission.ManageQuests | GuildPermission.SetMemberNotes |
                    GuildPermission.OrganizeEvents,
                    GuildPrivilege.All & ~GuildPrivilege.GuildSalary
                )},
                { GuildRankType.Leader, new GuildRank(
                    GuildRankType.Leader,
                    "Leader",
                    4,
                    GuildPermission.All,
                    GuildPrivilege.All
                )}
            };
        }

        public IReadOnlyList<IGuildMember> GetMembers()
        {
            return _members.Values.ToList();
        }

        public IGuildMember GetMember(int entityId)
        {
            return _members.TryGetValue(entityId, out var member) ? member : null;
        }

        public bool IsMember(int entityId)
        {
            return _members.ContainsKey(entityId);
        }

        public IReadOnlyList<IGuildRank> GetRanks()
        {
            return _ranks.Values.ToList();
        }

        public IGuildRank GetRank(GuildRankType rankType)
        {
            return _ranks.TryGetValue(rankType, out var rank) ? rank : null;
        }

        public bool IsFull()
        {
            return _members.Count >= _data.MaxMembers;
        }

        public void SetMessageOfTheDay(string motd)
        {
            // This would update the underlying data structure
            // In a real implementation, this would persist to storage
        }

        public void AddExperience(long amount)
        {
            // Add experience and check for level up
            // In a real implementation, this would update the data and check thresholds
        }

        /// <summary>
        /// Add a member to the guild.
        /// Internal method used by GuildManager.
        /// </summary>
        internal void AddMemberInternal(IGuildMember member)
        {
            if (!_members.ContainsKey(member.EntityId))
            {
                _members[member.EntityId] = member;
                if (!_data.MemberIds.Contains(member.EntityId))
                {
                    _data.MemberIds.Add(member.EntityId);
                }
            }
        }

        /// <summary>
        /// Remove a member from the guild.
        /// Internal method used by GuildManager.
        /// </summary>
        internal void RemoveMemberInternal(int entityId)
        {
            _members.Remove(entityId);
            _data.MemberIds.Remove(entityId);
        }

        public override string ToString()
        {
            return $"Guild[{GuildId}]: {Name} [{Tag}] - Level {Level}, {MemberCount}/{MaxMembers} members";
        }
    }
}
