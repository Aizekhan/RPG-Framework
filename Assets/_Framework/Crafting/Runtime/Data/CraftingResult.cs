using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Represents a single result item from crafting.
    /// </summary>
    [Serializable]
    public struct CraftingResult : ICraftingResult
    {
        /// <summary>
        /// Item ID of the crafted result.
        /// </summary>
        public string ItemId { get; set; }

        /// <summary>
        /// Quantity of this item produced.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Success chance for this result (0-100).
        /// 100 means guaranteed, 0 means impossible.
        /// </summary>
        public float SuccessChance { get; set; }

        /// <summary>
        /// Create a new crafting result.
        /// </summary>
        public CraftingResult(string itemId, int quantity, float successChance = 100f)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
            if (successChance < 0 || successChance > 100)
                throw new ArgumentException("Success chance must be between 0 and 100", nameof(successChance));

            ItemId = itemId;
            Quantity = quantity;
            SuccessChance = successChance;
        }
    }
}
