namespace RPGFramework.Trading
{
    /// <summary>
    /// The game's inventory as the trading manager sees it: lets a trade take the items it requests and give the
    /// items it offers. The game implements it over its own inventory (Items module, save profile).
    /// </summary>
    public interface ITradeInventory
    {
        /// <summary>
        /// True when the owner holds at least <paramref name="quantity"/> of the item.
        /// </summary>
        bool HasItem(int ownerId, string itemId, int quantity);

        /// <summary>
        /// Takes the items from the owner; false, with nothing taken, when it cannot.
        /// </summary>
        bool RemoveItem(int ownerId, string itemId, int quantity);

        /// <summary>
        /// Gives the items to the owner; false, with nothing given, when it cannot (a full inventory).
        /// </summary>
        bool AddItem(int ownerId, string itemId, int quantity);
    }
}
