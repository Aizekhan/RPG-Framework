namespace RPGFramework.Crafting
{
    /// <summary>
    /// Interface for crafting ingredients.
    /// Represents a single ingredient required for crafting.
    /// </summary>
    public interface ICraftingIngredient
    {
        /// <summary>
        /// Item ID of the ingredient.
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Quantity of this ingredient required.
        /// </summary>
        int Quantity { get; }
    }
}
