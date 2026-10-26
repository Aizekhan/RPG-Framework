using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Factions;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuildManager.
    /// Manages guild lifecycle, membership, and operations.
    /// </summary>
    public class GuildManager : IGuildManager
    {
        private readonly IGuildRegistry _registry;
        private readonly IEntityManager _entityManager;
        private readonly IFactionRegistry _factionRegistry;
        private readonly Dictionary<int, string> _entityToGuildMap; // entityId -> guildId

        /// <summary>
        /// Create a new guild manager.
        /// </summary>
        public GuildManager(IEntityManager entityManager, IFactionRegistry factionRegistry, IGuildRegistry registry)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _factionRegistry = factionRegistry ?? throw new ArgumentNullException(nameof(factionRegistry));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _entityToGuildMap = new Dictionary<int, string>();
        }

        public IGuild CreateGuild(string guildId, string name, string tag, int leaderId, string description = "", int maxMembers = 100)
        {
            if (string.IsNullOrEmpty(guildId))
                throw new ArgumentException("Guild ID cannot be null or empty", nameof(guildId));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Guild name cannot be null or empty", nameof(name));
            if (_registry.HasGuild(guildId))
                throw new InvalidOperationException($"Guild with ID '{guildId}' already exists");
            if (_registry.IsNameTaken(name))
                throw new InvalidOperationException($"Guild name '{name}' is already taken");
            if (_registry.IsTagTaken(tag))
                throw new InvalidOperationException($"Guild tag '{tag}' is already taken");

            // Create guild data
            var guildData = GuildData.Create(guildId, name, tag, leaderId, description, maxMembers);
            var guild = new Guild(guildData);

            // Register guild
            _registry.RegisterGuild(guild);

            // Add leader as first member
            var leaderData = GuildMemberData.Create(guildId, GuildRankType.Leader);
            var leaderMember = new GuildMember(leaderId, guild, leaderData);
            ((Guild)guild).AddMemberInternal(leaderMember);

            // Update entity component
            _entityManager.AddComponent(leaderId, leaderData);
            _entityToGuildMap[leaderId] = guildId;

            return guild;
        }

        public void DisbandGuild(string guildId)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                throw new InvalidOperationException($"Guild '{guildId}' not found");

            // Remove all members, including the leader (RemoveMember refuses the leader while others remain)
            foreach (var member in guild.GetMembers().ToList())
            {
                ((Guild)guild).RemoveMemberInternal(member.EntityId);
                ClearMembership(member.EntityId);
            }

            // Unregister guild
            _registry.UnregisterGuild(guildId);
        }

        public IGuild GetGuild(string guildId)
        {
            return _registry.GetGuild(guildId);
        }

        public IGuild GetGuildForEntity(int entityId)
        {
            if (_entityToGuildMap.TryGetValue(entityId, out string guildId))
            {
                return _registry.GetGuild(guildId);
            }

            // Fallback: check component
            if (_entityManager.HasComponent<GuildMemberData>(entityId))
            {
                var data = _entityManager.GetComponent<GuildMemberData>(entityId);
                if (data.IsInGuild())
                {
                    _entityToGuildMap[entityId] = data.GuildId;
                    return _registry.GetGuild(data.GuildId);
                }
            }

            return null;
        }

        public IReadOnlyList<IGuild> GetAllGuilds()
        {
            return _registry.GetAllGuilds();
        }

        public bool AddMember(string guildId, int entityId, GuildRankType rank = GuildRankType.Initiate)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                return false;

            if (guild.IsFull())
                return false;

            if (guild.IsMember(entityId))
                return false; // Already a member

            // Check if entity is in another guild
            if (IsInGuild(entityId))
                return false;

            // Create member data
            var memberData = GuildMemberData.Create(guildId, rank);
            var member = new GuildMember(entityId, guild, memberData);

            // Add to guild
            ((Guild)guild).AddMemberInternal(member);

            // Update entity component
            _entityManager.AddComponent(entityId, memberData);
            _entityToGuildMap[entityId] = guildId;

            return true;
        }

        public bool RemoveMember(string guildId, int entityId)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                return false;

            if (!guild.IsMember(entityId))
                return false;

            // Cannot remove the leader without transferring leadership
            if (guild.LeaderId == entityId && guild.MemberCount > 1)
                return false;

            // Remove from guild
            ((Guild)guild).RemoveMemberInternal(entityId);

            ClearMembership(entityId);

            // If this was the last member, disband the guild
            if (guild.MemberCount == 0)
            {
                _registry.UnregisterGuild(guildId);
            }

            return true;
        }

        public bool PromoteMember(string guildId, int entityId, GuildRankType newRank)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                return false;

            var member = guild.GetMember(entityId);
            if (member == null)
                return false;

            if (newRank <= member.Rank)
                return false; // Not a promotion

            // Update member rank
            ((GuildMember)member).SetRank(newRank);

            // Update component
            var data = ((GuildMember)member).GetData();
            _entityManager.AddComponent(entityId, data);

            return true;
        }

        public bool DemoteMember(string guildId, int entityId, GuildRankType newRank)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                return false;

            var member = guild.GetMember(entityId);
            if (member == null)
                return false;

            if (newRank >= member.Rank)
                return false; // Not a demotion

            // Cannot demote the leader
            if (member.Rank == GuildRankType.Leader)
                return false;

            // Update member rank
            ((GuildMember)member).SetRank(newRank);

            // Update component
            var data = ((GuildMember)member).GetData();
            _entityManager.AddComponent(entityId, data);

            return true;
        }

        public bool TransferLeadership(string guildId, int newLeaderId)
        {
            var guild = _registry.GetGuild(guildId);
            if (guild == null)
                return false;

            var newLeader = guild.GetMember(newLeaderId);
            if (newLeader == null)
                return false; // New leader must be a member

            var oldLeader = guild.GetMember(guild.LeaderId);
            if (oldLeader != null)
            {
                // Demote old leader to Elder
                ((GuildMember)oldLeader).SetRank(GuildRankType.Elder);
                var oldData = ((GuildMember)oldLeader).GetData();
                _entityManager.AddComponent(guild.LeaderId, oldData);
            }

            // Promote new leader
            ((GuildMember)newLeader).SetRank(GuildRankType.Leader);
            var newData = ((GuildMember)newLeader).GetData();
            _entityManager.AddComponent(newLeaderId, newData);

            // Update guild leader
            // In a real implementation, this would update the GuildData structure

            return true;
        }

        public bool HasPermission(int entityId, GuildPermission permission)
        {
            var member = GetMember(entityId);
            return member != null && member.HasPermission(permission);
        }

        public bool HasPrivilege(int entityId, GuildPrivilege privilege)
        {
            var member = GetMember(entityId);
            return member != null && member.HasPrivilege(privilege);
        }

        public IGuildMember GetMember(int entityId)
        {
            var guild = GetGuildForEntity(entityId);
            return guild?.GetMember(entityId);
        }

        public bool IsInGuild(int entityId)
        {
            if (_entityToGuildMap.ContainsKey(entityId))
                return true;

            if (_entityManager.HasComponent<GuildMemberData>(entityId))
            {
                var data = _entityManager.GetComponent<GuildMemberData>(entityId);
                return data.IsInGuild();
            }

            return false;
        }

        private void ClearMembership(int entityId)
        {
            var memberData = GuildMemberData.Create(string.Empty);
            memberData.Clear();
            _entityManager.AddComponent(entityId, memberData);
            _entityToGuildMap.Remove(entityId);
        }
    }
}
