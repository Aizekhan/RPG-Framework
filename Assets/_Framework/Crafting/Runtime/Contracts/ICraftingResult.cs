namespace RPGFramework.Crafting
{
    /// <summary>
    /// Interface for a crafting result item.
    /// Represents what an item that can be produced from crafting.
    /// </summary>
    public interface ICraftingResult
    {
        /// <summary>
        /// Item ID of the crafted result.
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Quantity of this item produced.
        /// </summary>
        int Quantity { get; }

        /// <summary>
        /// Success chance for this result (0-100).
        /// 100 means guaranteed, 0 means impossible.
        /// </summary>
        float SuccessChance { get; }
    }
}
