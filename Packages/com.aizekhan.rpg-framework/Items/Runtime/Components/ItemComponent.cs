using RPGFramework.ECS;

namespace RPGFramework.Items
{
    /// <summary>
    /// Component that marks an entity as representing an item in the world.
    /// Used for dropped items, loot bags, quest objects, etc.
    /// Stores the item instance and pickup/despawn metadata.
    /// </summary>
    public struct ItemComponent : IComponent
    {
        /// <summary>
        /// Item instance data.
        /// Contains the actual item and its runtime state.
        /// </summary>
        public IItemInstance ItemInstance;

        /// <summary>
        /// Whether this item can be picked up by players.
        /// False for decorative items or quest items that require conditions.
        /// </summary>
        public bool IsPickupable;

        /// <summary>
        /// Entity ID of the owner (who dropped this item).
        /// -1 = no owner (world loot, spawned item).
        /// Used for loot reservation systems.
        /// </summary>
        public int OwnerEntityId;

        /// <summary>
        /// Timestamp when this item becomes available to all players.
        /// Used for loot reservation (owner gets priority pickup window).
        /// 0 = immediately available.
        /// </summary>
        public float AvailableAt;

        /// <summary>
        /// Timestamp when this item despawns automatically.
        /// -1 = never despawns (permanent world item).
        /// Used to clean up dropped loot after timeout.
        /// </summary>
        public float DespawnAt;

        /// <summary>
        /// Factory: Create item component from instance.
        /// </summary>
        /// <param name="instance">Item instance</param>
        /// <param name="isPickupable">Can be picked up (default: true)</param>
        /// <returns>New item component</returns>
        public static ItemComponent Create(IItemInstance instance, bool isPickupable = true)
        {
            return new ItemComponent
            {
                ItemInstance = instance,
                IsPickupable = isPickupable,
                OwnerEntityId = -1,
                AvailableAt = 0f,
                DespawnAt = -1f
            };
        }

        /// <summary>
        /// Factory: Create with owner and loot reservation.
        /// </summary>
        /// <param name="instance">Item instance</param>
        /// <param name="ownerEntityId">Entity ID of owner</param>
        /// <param name="reservationDuration">How long to reserve for owner (seconds)</param>
        /// <param name="currentTime">Current game time</param>
        /// <returns>New item component with reservation</returns>
        public static ItemComponent CreateWithOwner(
            IItemInstance instance,
            int ownerEntityId,
            float reservationDuration,
            float currentTime)
        {
            return new ItemComponent
            {
                ItemInstance = instance,
                IsPickupable = true,
                OwnerEntityId = ownerEntityId,
                AvailableAt = currentTime + reservationDuration,
                DespawnAt = -1f
            };
        }

        /// <summary>
        /// Factory: Create with despawn timer.
        /// </summary>
        /// <param name="instance">Item instance</param>
        /// <param name="despawnDuration">Time until despawn (seconds)</param>
        /// <param name="currentTime">Current game time</param>
        /// <returns>New item component with despawn timer</returns>
        public static ItemComponent CreateWithDespawn(
            IItemInstance instance,
            float despawnDuration,
            float currentTime)
        {
            return new ItemComponent
            {
                ItemInstance = instance,
                IsPickupable = true,
                OwnerEntityId = -1,
                AvailableAt = 0f,
                DespawnAt = currentTime + despawnDuration
            };
        }
    }
}
