using System;
using RPGFramework.ActionRpg;
using RPGFramework.Events;

namespace ReferenceGame
{
    /// <summary>
    /// Tunable numbers of the kill streak rule. Serializable so the host shows them in the inspector.
    /// </summary>
    [Serializable]
    public sealed class KillStreakSettings
    {
        /// <summary>
        /// Kills in a row, without the hero being hurt, that earn a bonus. 0 turns the rule off.
        /// </summary>
        public int KillsForBonus = 3;

        public string BonusItemId = "goblin_ear";

        public int BonusQuantity = 1;
    }

    /// <summary>
    /// A streak of kills earned a bonus.
    /// </summary>
    public readonly struct KillStreakRewardedEvent : IEvent
    {
        public KillStreakRewardedEvent(int streak, string itemId, int quantity)
        {
            Streak = streak;
            ItemId = itemId;
            Quantity = quantity;
        }

        public int Streak { get; }
        public string ItemId { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Goblin Valley's own rule, the kind the inspector cannot express: every <see cref="KillStreakSettings.KillsForBonus"/>
    /// kills in a row without the hero taking damage give a bonus item. Everything else in the game is the framework's
    /// action RPG built from the Game Setup asset.
    /// </summary>
    public sealed class KillStreak : IDisposable
    {
        private readonly ActorWorld _world;
        private readonly IEventBus _bus;
        private readonly KillStreakSettings _settings;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public KillStreak(ActorWorld world, IEventBus bus, KillStreakSettings settings)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _settings = settings ?? new KillStreakSettings();
        }

        /// <summary>
        /// Kills since the hero was last hurt.
        /// </summary>
        public int Current { get; private set; }

        public int Best { get; private set; }

        public void Start()
        {
            _bus.Subscribe<ActorKilledEvent>(OnKilled).AddTo(_subscriptions);
            _bus.Subscribe<ActorDamagedEvent>(OnDamaged).AddTo(_subscriptions);
        }

        private void OnKilled(ActorKilledEvent killed)
        {
            if (!killed.ByPlayer)
                return;
            Current++;
            Best = Math.Max(Best, Current);

            int every = _settings.KillsForBonus;
            if (every <= 0 || Current % every != 0)
                return;
            if (_world.GiveItem(_settings.BonusItemId, _settings.BonusQuantity))
                _world.Announce($"Kill streak x{Current}! Bonus loot");
            _bus.Publish(new KillStreakRewardedEvent(Current, _settings.BonusItemId, _settings.BonusQuantity));
        }

        private void OnDamaged(ActorDamagedEvent damaged)
        {
            if (damaged.TargetId == _world.PlayerId && damaged.Amount > 0f)
                Current = 0;
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
