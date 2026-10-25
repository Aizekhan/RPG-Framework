using System;
using System.Collections.Generic;
using RPGFramework.Housing.Runtime.Data;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Manages guest access and permissions for houses.
    /// </summary>
    public interface IHousePermissions
    {
        /// <summary>
        /// ID of the house these permissions apply to.
        /// </summary>
        Guid HouseId { get; }

        /// <summary>
        /// Entity ID of the house owner.
        /// </summary>
        Guid OwnerId { get; }

        /// <summary>
        /// All granted permissions (entity ID -> permission level).
        /// </summary>
        IReadOnlyDictionary<Guid, PermissionLevel> Permissions { get; }

        /// <summary>
        /// Grant permission to an entity.
        /// </summary>
        void GrantPermission(Guid entityId, PermissionLevel level);

        /// <summary>
        /// Revoke permission from an entity.
        /// </summary>
        void RevokePermission(Guid entityId);

        /// <summary>
        /// Get permission level for an entity.
        /// </summary>
        PermissionLevel GetPermissionLevel(Guid entityId);

        /// <summary>
        /// Check if an entity has at least the specified permission level.
        /// </summary>
        bool HasPermission(Guid entityId, PermissionLevel requiredLevel);

        /// <summary>
        /// Check if an entity can modify furniture.
        /// </summary>
        bool CanModifyFurniture(Guid entityId);

        /// <summary>
        /// Check if an entity can access storage.
        /// </summary>
        bool CanAccessStorage(Guid entityId);
    }
}
