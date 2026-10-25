using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Represents a single ingredient required for crafting.
    /// </summary>
    [Serializable]
    public struct CraftingIngredient : ICraftingIngredient
    {
        /// <summary>
        /// Item ID of the ingredient.
        /// </summary>
        public string ItemId { get; set; }

        /// <summary>
        /// Quantity of this ingredient required.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Create a new crafting ingredient.
        /// </summary>
        public CraftingIngredient(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
