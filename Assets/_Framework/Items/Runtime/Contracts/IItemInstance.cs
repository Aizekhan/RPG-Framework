using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// Represents a unique instance of an item.
    /// Wraps item definition with instance-specific data.
    /// Each item in a player's inventory is represented by an instance.
    /// </summary>
    public interface IItemInstance
    {
        /// <summary>
        /// Unique runtime instance ID.
        /// Generated when item is created/dropped/looted.
        /// Used to track specific item instances across the game.
        /// </summary>
        string InstanceId { get; }

        /// <summary>
        /// Reference to the item definition ID.
        /// Links this instance to its base definition in the item registry.
        /// </summary>
        string ItemDefinitionId { get; }

        /// <summary>
        /// Current stack quantity for stackable items.
        /// 1 for non-stackable items (equipment).
        /// Can be 1 to MaxStackSize for stackable items.
        /// </summary>
        int Quantity { get; set; }

        /// <summary>
        /// Current durability points (for equipment).
        /// -1 = not applicable (consumables, materials).
        /// 0 = broken/destroyed equipment.
        /// Greater than 0 = current durability remaining.
        /// </summary>
        int CurrentDurability { get; set; }

        /// <summary>
        /// Timestamp when this instance was created.
        /// Can be used for item age, expiration, or sorting.
        /// </summary>
        float CreatedAt { get; }

        /// <summary>
        /// Custom instance properties (enchantments, sockets, modifications).
        /// Framework provides storage, game layer defines schema.
        /// Examples: {"SocketedGem": "RubyOfStrength", "Enchantment": "+10 Fire Damage"}
        /// </summary>
        Dictionary<string, object> CustomProperties { get; }
    }
}
