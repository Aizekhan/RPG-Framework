using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What a consumable does when a player uses it (<see cref="IConsumable.EffectId"/>, its magnitude): "RestoreHealth"
    /// heals that much, "RestoreHealthPercent" that share (0..1) of maximum health; an effect id of the game's status
    /// effects (<see cref="ActionRpgContent.StatusEffects"/>) puts it on the player (a magnitude above 0 is its power).
    /// Any other id is the game's own: <see cref="ConsumableUsedEvent"/> says it was used.
    /// </summary>
    public static class ConsumableEffects
    {
        public const string RestoreHealth = "RestoreHealth";
        public const string RestoreHealthPercent = "RestoreHealthPercent";
    }

    /// <summary>
    /// A player used a consumable (a potion drunk, a scroll read); for effects of the game's own and the look.
    /// </summary>
    public readonly struct ConsumableUsedEvent : IEvent
    {
        public ConsumableUsedEvent(int playerId, string itemId, string effectId, float magnitude)
        {
            PlayerId = playerId;
            ItemId = itemId;
            EffectId = effectId;
            Magnitude = magnitude;
        }

        public int PlayerId { get; }
        public string ItemId { get; }
        public string EffectId { get; }
        public float Magnitude { get; }
    }

    /// <summary>
    /// Players use consumables from their backpacks (<see cref="ActorWorld.Consumables"/>): the effect lands, one is
    /// used up (unless it is not consumed on use) and its cooldown starts, shared by its cooldown category (every
    /// "Potion") or its own item. Cooldowns run while the game is not paused.
    /// </summary>
    public sealed class ActorConsumables : IUpdateSystem
    {
        private readonly ActorWorld _world;
        private readonly IEventPublisher _events;
        private readonly Dictionary<(int, string), float> _readyAt = new Dictionary<(int, string), float>();

        internal ActorConsumables(ActorWorld world, IEventPublisher events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
        }

        /// <summary>
        /// Seconds the consumables have been running (not while paused).
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// Seconds until the player can use this item again; 0 when ready.
        /// </summary>
        public float CooldownLeft(int playerId, string itemId)
        {
            var consumable = _world.ItemOf(itemId) as IConsumable;
            if (consumable == null)
                return 0f;
            return _readyAt.TryGetValue((playerId, CooldownKey(consumable)), out float at) ? Math.Max(0f, at - Time) : 0f;
        }

        /// <summary>
        /// Why the player cannot use this item now, or null when they can.
        /// </summary>
        public string WhyNot(int playerId, string itemId)
        {
            var player = _world.Get(playerId);
            if (player == null || _world.ProgressOwnerOf(playerId) < 0)
                return "Not a player in the party";
            if (!player.IsAlive())
                return "Fallen";
            if (!(_world.ItemOf(itemId) is IConsumable consumable))
                return "Not something to use";
            if ((_world.InventoryOf(playerId)?.GetAllItems().Count(i => i != null && i.ItemId == itemId) ?? 0) == 0)
                return "None in the backpack";
            if (CooldownLeft(playerId, itemId) > 0f)
                return "Not ready yet";
            switch (consumable.EffectId)
            {
                case ConsumableEffects.RestoreHealth:
                case ConsumableEffects.RestoreHealthPercent:
                    return player.Health >= player.MaxHealth ? "Already at full health" : null;
            }
            if (IsStatusEffect(consumable.EffectId) && _world.Effects == null)
                return "Effects are off in this game";
            return null;
        }

        /// <summary>
        /// Uses one of the item; false (nothing changes) when <see cref="WhyNot"/> says why.
        /// </summary>
        public bool Use(int playerId, string itemId)
        {
            if (WhyNot(playerId, itemId) != null)
                return false;
            var consumable = (IConsumable)_world.ItemOf(itemId);
            var player = _world.Get(playerId);
            switch (consumable.EffectId)
            {
                case ConsumableEffects.RestoreHealth:
                    _world.Heal(playerId, consumable.EffectMagnitude);
                    break;
                case ConsumableEffects.RestoreHealthPercent:
                    _world.Heal(playerId, player.MaxHealth * consumable.EffectMagnitude);
                    break;
                default:
                    if (IsStatusEffect(consumable.EffectId))
                    {
                        bool applied = consumable.EffectMagnitude > 0f
                            ? _world.Effects.Apply(playerId, consumable.EffectId, playerId, consumable.EffectMagnitude)
                            : _world.Effects.Apply(playerId, consumable.EffectId, playerId);
                        if (!applied)
                            return false;
                    }
                    break;
            }
            if (consumable.IsConsumedOnUse)
                _world.TakeItem(playerId, itemId, 1);
            if (consumable.CooldownDuration > 0f)
                _readyAt[(playerId, CooldownKey(consumable))] = Time + consumable.CooldownDuration;
            _events?.Publish(new ConsumableUsedEvent(playerId, itemId, consumable.EffectId, consumable.EffectMagnitude));
            return true;
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            if (deltaTime > 0f && _world.IsStarted && !_world.IsPaused)
                Time += deltaTime;
        }

        public void Dispose()
        {
        }

        internal void Forget(int playerId)
        {
            foreach (var key in _readyAt.Keys.Where(k => k.Item1 == playerId).ToList())
                _readyAt.Remove(key);
        }

        private static string CooldownKey(IConsumable consumable) =>
            string.IsNullOrEmpty(consumable.CooldownCategory) ? "item:" + consumable.ItemId : "category:" + consumable.CooldownCategory;

        private bool IsStatusEffect(string effectId) =>
            !string.IsNullOrEmpty(effectId) && (_world.Content?.StatusEffects.Any(e => e != null && e.Id == effectId) ?? false);
    }
}
