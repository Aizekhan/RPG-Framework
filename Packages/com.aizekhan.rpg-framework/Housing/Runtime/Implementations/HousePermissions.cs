using System;
using System.Collections.Generic;
using RPGFramework.Events;
using RPGFramework.Housing.Runtime.Contracts;
using RPGFramework.Housing.Runtime.Data;
using RPGFramework.Housing.Runtime.Events;

namespace RPGFramework.Housing.Runtime.Implementations
{
    /// <summary>
    /// Implementation of IHousePermissions managing guest access.
    /// With an event publisher, raises GuestInvited when an entity is granted a new access level.
    /// </summary>
    public class HousePermissions : IHousePermissions
    {
        private readonly Dictionary<Guid, PermissionLevel> _permissions;
        private readonly IEventPublisher _events;

        public Guid HouseId { get; }
        public Guid OwnerId { get; }
        public IReadOnlyDictionary<Guid, PermissionLevel> Permissions => _permissions;

        /// <summary>
        /// Create a new house permissions instance.
        /// </summary>
        public HousePermissions(Guid houseId, Guid ownerId, IEventPublisher events = null)
        {
            _events = events;
            HouseId = houseId;
            OwnerId = ownerId;
            _permissions = new Dictionary<Guid, PermissionLevel>
            {
                { ownerId, PermissionLevel.Owner }
            };
        }

        public void GrantPermission(Guid entityId, PermissionLevel level)
        {
            if (entityId == OwnerId)
                return; // Cannot change owner's permission

            var previousLevel = GetPermissionLevel(entityId);
            _permissions[entityId] = level;

            // Banned grants no access, and re-granting the same level invites no one
            if (level > PermissionLevel.Banned && level != previousLevel)
                _events?.Publish(new GuestInvitedEvent(HouseId, entityId, level, Guid.Empty));
        }

        public void RevokePermission(Guid entityId)
        {
            if (entityId == OwnerId)
                return; // Cannot revoke owner's permission

            _permissions.Remove(entityId);
        }

        public PermissionLevel GetPermissionLevel(Guid entityId)
        {
            if (_permissions.TryGetValue(entityId, out var level))
                return level;

            return PermissionLevel.Banned;
        }

        public bool HasPermission(Guid entityId, PermissionLevel requiredLevel)
        {
            var currentLevel = GetPermissionLevel(entityId);
            return (int)currentLevel >= (int)requiredLevel;
        }

        public bool CanModifyFurniture(Guid entityId)
        {
            return HasPermission(entityId, PermissionLevel.CoOwner);
        }

        public bool CanAccessStorage(Guid entityId)
        {
            return HasPermission(entityId, PermissionLevel.Resident);
        }
    }
}
