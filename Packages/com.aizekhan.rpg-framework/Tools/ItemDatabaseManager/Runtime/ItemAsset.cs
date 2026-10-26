using UnityEngine;
using RPGFramework.Items;

namespace RPGFramework.Tools.ItemDatabaseManager
{
    /// <summary>
    /// ScriptableObject for visual item definition authoring.
    /// Supports Equipment and Consumable item types.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "RPG Framework/Item", order = 103)]
    public class ItemAsset : ScriptableObject
    {
        [Header("Item Identity")]
        [Tooltip("Unique identifier for this item")]
        public string itemId = "item_new";

        [Tooltip("Display name of the item")]
        public string itemName = "New Item";

        [Tooltip("Description text for tooltips/UI")]
        [TextArea(3, 6)]
        public string description = "Item description...";

        [Tooltip("Icon identifier for UI rendering")]
        public string iconId = "icon_default";

        [Tooltip("Picture shown in the backpack; empty shows the item's initials")]
        public Sprite icon;

        [Header("Item Properties")]
        [Tooltip("Category of this item")]
        public ItemType itemType = ItemType.Equipment;

        [Tooltip("Rarity tier")]
        public ItemRarity rarity = ItemRarity.Common;

        [Tooltip("Quality tier")]
        public ItemQuality quality = ItemQuality.Normal;

        [Tooltip("Maximum stack size (1 = not stackable)")]
        [Min(1)]
        public int maxStackSize = 1;

        [Tooltip("Item level/tier")]
        [Min(1)]
        public int itemLevel = 1;

        [Header("Equipment Properties")]
        [Tooltip("Equipment slot (for Equipment type)")]
        public string equipmentSlot = "MainHand";

        [Tooltip("Stat modifiers (for Equipment type)")]
        public ItemStatModifierEntry[] statModifiers = new ItemStatModifierEntry[0];

        [Header("Consumable Properties")]
        [Tooltip("What using it does: the effect id your game's effect system knows (for Consumable type)")]
        public string effectId = "default_effect";

        [Tooltip("How strong the effect is, e.g. health restored (for Consumable type)")]
        public float effectMagnitude = 10f;

        [Tooltip("Use duration in seconds (for Consumable type)")]
        [Min(0f)]
        public float useDuration = 0f;

        [Tooltip("Cooldown in seconds (for Consumable type)")]
        [Min(0f)]
        public float cooldown = 0f;

        [Tooltip("Whether consumed on use (for Consumable type)")]
        public bool consumeOnUse = true;

        /// <summary>
        /// Converts to Framework IItem implementation: a consumable becomes an <see cref="IConsumable"/>; equipment
        /// becomes an unstackable item, and its slot and stat modifiers stay on the asset (<see cref="equipmentSlot"/>,
        /// <see cref="statModifiers"/>) for the game, which owns its slot and stat types.
        /// </summary>
        public IItem ToItem()
        {
            return itemType switch
            {
                ItemType.Equipment => ToEquipment(),
                ItemType.Consumable => ToConsumable(),
                _ => ToBaseItem()
            };
        }

        private IItem ToBaseItem()
        {
            return new Item(
                itemId: itemId,
                name: itemName,
                description: description,
                itemType: itemType,
                rarity: rarity,
                quality: quality,
                maxStackSize: maxStackSize,
                itemLevel: itemLevel,
                iconId: iconId
            );
        }

        private IItem ToEquipment()
        {
            // Tools module returns base IItem
            // Game layer implements concrete Equipment with specific slot/stat types
            return new Item(
                itemId: itemId,
                name: itemName,
                description: description,
                itemType: ItemType.Equipment,
                rarity: rarity,
                quality: quality,
                maxStackSize: 1, // Equipment is not stackable
                itemLevel: itemLevel,
                iconId: iconId
            );
        }

        private IConsumable ToConsumable()
        {
            return new Consumable(
                itemId: itemId,
                name: itemName,
                description: description,
                effectId: effectId,
                effectMagnitude: effectMagnitude,
                rarity: rarity,
                quality: quality,
                maxStackSize: maxStackSize,
                itemLevel: itemLevel,
                cooldownDuration: cooldown,
                isConsumedOnUse: consumeOnUse,
                iconId: iconId
            );
        }

        /// <summary>
        /// Validates item configuration.
        /// </summary>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(itemId))
                errors.Add("Item ID cannot be empty");

            if (string.IsNullOrWhiteSpace(itemName))
                errors.Add("Item Name cannot be empty");

            if (maxStackSize < 1)
                errors.Add("Max Stack Size must be at least 1");

            if (itemLevel < 1)
                errors.Add("Item Level must be at least 1");

            // Equipment-specific validation
            if (itemType == ItemType.Equipment)
            {
                if (string.IsNullOrWhiteSpace(equipmentSlot))
                    errors.Add("Equipment Slot is required for Equipment items");

                if (maxStackSize != 1)
                    errors.Add("Equipment items should not be stackable (Max Stack Size = 1)");
            }

            // Consumable-specific validation
            if (itemType == ItemType.Consumable)
            {
                if (useDuration < 0f)
                    errors.Add("Use Duration cannot be negative");

                if (cooldown < 0f)
                    errors.Add("Cooldown cannot be negative");
            }

            return errors;
        }

        private void OnValidate()
        {
            // Ensure IDs are sanitized
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                itemId = itemId.ToLower().Replace(" ", "_");
            }

            // Ensure valid values
            if (maxStackSize < 1) maxStackSize = 1;
            if (itemLevel < 1) itemLevel = 1;
            if (useDuration < 0f) useDuration = 0f;
            if (cooldown < 0f) cooldown = 0f;

            // Equipment should not be stackable
            if (itemType == ItemType.Equipment && maxStackSize != 1)
            {
                maxStackSize = 1;
            }
        }
    }

    /// <summary>
    /// Stat modifier for equipment items.
    /// </summary>
    [System.Serializable]
    public class ItemStatModifierEntry
    {
        [Tooltip("Stat name (e.g., Health, Damage, Defense)")]
        public string statName = "StatName";

        [Tooltip("Modifier value")]
        public float value = 10f;
    }
}
