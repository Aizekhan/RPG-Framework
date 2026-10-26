// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Contracts/IEquipmentSlot.cs

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Generic equipment slot for equippable items.
    /// Supports any slot type enum and item type.
    /// </summary>
    /// <typeparam name="TSlotType">Enum type for slot categories (e.g., ArmorSlot, WeaponSlot)</typeparam>
    /// <typeparam name="TItem">Type of items that can be equipped</typeparam>
    public interface IEquipmentSlot<TSlotType, TItem> where TSlotType : System.Enum
    {
        /// <summary>
        /// The type/category of this equipment slot.
        /// </summary>
        TSlotType SlotType { get; }

        /// <summary>
        /// Currently equipped item (default(TItem) if empty).
        /// </summary>
        TItem EquippedItem { get; }

        /// <summary>
        /// Whether this slot has an item equipped.
        /// </summary>
        bool IsOccupied { get; }

        /// <summary>
        /// Equips an item to this slot.
        /// Replaces any currently equipped item.
        /// </summary>
        /// <param name="item">Item to equip</param>
        /// <returns>Previously equipped item (default if slot was empty)</returns>
        TItem Equip(TItem item);

        /// <summary>
        /// Unequips the current item.
        /// </summary>
        /// <returns>The unequipped item (default if slot was empty)</returns>
        TItem Unequip();

        /// <summary>
        /// Checks if an item can be equipped to this slot.
        /// Validation can include item type restrictions, stat requirements, etc.
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if item can be equipped</returns>
        bool CanEquip(TItem item);
    }
}
