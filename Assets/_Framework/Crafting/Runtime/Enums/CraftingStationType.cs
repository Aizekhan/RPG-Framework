namespace RPGFramework.Crafting
{
    /// <summary>
    /// Types of crafting stations available in the game.
    /// Each station type may support different recipes and require specific ingredients.
    /// </summary>
    public enum CraftingStationType
    {
        /// <summary>
        /// Forge for metalworking and weapon crafting.
        /// </summary>
        Forge = 0,

        /// <summary>
        /// Alchemy laboratory for potion and reagent crafting.
        /// </summary>
        Alchemy = 1,

        /// <summary>
        /// Kitchen for food and cooking recipes.
        /// </summary>
        Cooking = 2,

        /// <summary>
        /// Enchanting station for magical item enhancement.
        /// </summary>
        Enchanting = 3,

        /// <summary>
        /// Carpentry workbench for wooden items and furniture.
        /// </summary>
        Carpentry = 4
    }
}
