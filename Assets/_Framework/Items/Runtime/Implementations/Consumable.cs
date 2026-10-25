namespace RPGFramework.Items
{
    /// <summary>
    /// Default implementation of IConsumable.
    /// Represents single-use items that apply effects when consumed.
    /// </summary>
    public class Consumable : Item, IConsumable
    {
        public string CooldownCategory { get; }
        public float CooldownDuration { get; }
        public bool UsableInCombat { get; }
        public bool IsConsumedOnUse { get; }
        public string EffectId { get; }
        public float EffectMagnitude { get; }

        /// <summary>
        /// Create a new consumable definition.
        /// </summary>
        /// <param name="itemId">Unique item identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="description">Tooltip description</param>
        /// <param name="effectId">Effect type identifier</param>
        /// <param name="effectMagnitude">Effect strength/amount</param>
        /// <param name="rarity">Rarity tier (default: Common)</param>
        /// <param name="quality">Quality tier (default: Normal)</param>
        /// <param name="maxStackSize">Maximum stack size (default: 20)</param>
        /// <param name="itemLevel">Item level (default: 1)</param>
        /// <param name="cooldownCategory">Cooldown category (default: empty = no shared cooldown)</param>
        /// <param name="cooldownDuration">Cooldown in seconds (default: 0 = no cooldown)</param>
        /// <param name="usableInCombat">Can use in combat (default: true)</param>
        /// <param name="isConsumedOnUse">Consumed on use (default: true)</param>
        /// <param name="iconId">Icon identifier (default: empty)</param>
        public Consumable(
            string itemId,
            string name,
            string description,
            string effectId,
            float effectMagnitude,
            ItemRarity rarity = ItemRarity.Common,
            ItemQuality quality = ItemQuality.Normal,
            int maxStackSize = 20,
            int itemLevel = 1,
            string cooldownCategory = "",
            float cooldownDuration = 0f,
            bool usableInCombat = true,
            bool isConsumedOnUse = true,
            string iconId = "")
            : base(itemId, name, description, ItemType.Consumable, rarity, quality, maxStackSize, itemLevel, iconId)
        {
            EffectId = effectId;
            EffectMagnitude = effectMagnitude;
            CooldownCategory = cooldownCategory;
            CooldownDuration = cooldownDuration;
            UsableInCombat = usableInCombat;
            IsConsumedOnUse = isConsumedOnUse;
        }
    }
}
