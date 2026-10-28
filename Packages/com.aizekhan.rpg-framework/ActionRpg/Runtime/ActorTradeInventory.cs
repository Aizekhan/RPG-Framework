using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Items;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Trades take items from and give items to the action RPG backpacks (<see cref="ActorWorld.InventoryOf"/>).
    /// Giving is all or nothing: a backpack that fills up halfway gets back what it had.
    /// </summary>
    public sealed class ActorTradeInventory : ITradeInventory
    {
        private readonly Func<ActorWorld> _world;

        public ActorTradeInventory(Func<ActorWorld> world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public bool HasItem(int ownerId, string itemId, int quantity) => Matching(ownerId, itemId).Count >= quantity;

        public bool RemoveItem(int ownerId, string itemId, int quantity)
        {
            var inventory = _world()?.InventoryOf(ownerId);
            var matching = Matching(ownerId, itemId);
            if (inventory == null || matching.Count < quantity)
                return false;
            foreach (var item in matching.Take(quantity))
                inventory.Remove(item);
            return true;
        }

        public bool AddItem(int ownerId, string itemId, int quantity)
        {
            var world = _world();
            var inventory = world?.InventoryOf(ownerId);
            if (inventory == null)
                return false;
            var added = new List<IItem>();
            var item = world.ItemRegistry.GetItem(itemId);
            if (item == null)
                return false;
            for (int i = 0; i < quantity; i++)
            {
                if (!inventory.Add(item))
                {
                    foreach (var back in added)
                        inventory.Remove(back);
                    return false;
                }
                added.Add(item);
            }
            return true;
        }

        private List<IItem> Matching(int ownerId, string itemId) =>
            _world()?.InventoryOf(ownerId)?.GetAllItems().Where(i => i != null && i.ItemId == itemId).ToList() ?? new List<IItem>();
    }
}
