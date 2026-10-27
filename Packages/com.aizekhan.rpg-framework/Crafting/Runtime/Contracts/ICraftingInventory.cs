namespace RPGFramework.Crafting
{
    /// <summary>
    /// Where crafters keep their items, as the crafting manager needs it. The game connects its own inventory: with
    /// one, a craft starts only when the ingredients are there, takes them at the start, gives them back when it is
    /// cancelled and hands out what was made. Without one, the game does all of that on the manager's events.
    /// </summary>
    public interface ICraftingInventory
    {
        bool Has(int entityId, string itemId, int quantity);

        /// <returns>False when the items are not there (nothing is taken then).</returns>
        bool Take(int entityId, string itemId, int quantity);

        void Give(int entityId, string itemId, int quantity);
    }
}
