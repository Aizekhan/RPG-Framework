using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Items;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Trades in the action RPG cost what <see cref="ActorWorld.TradePricing"/> says for the party member trading.
    /// </summary>
    public sealed class ActorTradePricing : ITradePricing
    {
        private readonly Func<ActorWorld> _world;

        public ActorTradePricing(Func<ActorWorld> world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// The world's <see cref="ActorWorld.TradePricing"/> for the party member trading (the wallet's owner when the
        /// trade did not come through the world), or the offer's own gold; a faction's trader then moves it with the
        /// player's standing (<see cref="ActorReputation.PriceFor"/>).
        /// </summary>
        public long GoldFor(int playerEntityId, ITrader trader, ITradeOffer offer)
        {
            var world = _world();
            if (world == null)
                return offer.GoldAmount;
            int player = world.TradingPlayer >= 0 ? world.TradingPlayer : playerEntityId;
            long gold = world.TradePricing?.GoldFor(player, trader, offer) ?? offer.GoldAmount;
            return world.Reputation.PriceFor(player, trader?.TraderId, gold);
        }
    }

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
            var item = world.ItemOf(itemId);
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
