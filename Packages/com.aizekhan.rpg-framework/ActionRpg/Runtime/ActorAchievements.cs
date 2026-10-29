using System;
using RPGFramework.Achievements;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Feeds the players' deeds in an <see cref="ActorWorld"/> to their achievements
    /// (<see cref="ActionRpgContent.Achievements"/>, <see cref="ActorWorld.Achievements"/>): kills by character id
    /// (KillCount), items that come into their backpack by base item id (ItemCollected), completed quests
    /// (QuestCompleted), the level reached (LevelReached) and the damage their strikes deal (DamageDealt). Each player
    /// counts for themselves; nothing counts while a save is being put back.
    /// </summary>
    internal sealed class ActorAchievements : IDisposable
    {
        private readonly ActorWorld _world;
        private readonly AchievementTracker _tracker;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public ActorAchievements(ActorWorld world, AchievementTracker tracker, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            bus.Subscribe<ActorKilledEvent>(OnKilled).AddTo(_subscriptions);
            bus.Subscribe<ItemAddedEvent<IItem>>(OnItemAdded).AddTo(_subscriptions);
            bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted).AddTo(_subscriptions);
            bus.Subscribe<LevelUpEvent>(OnLevelUp).AddTo(_subscriptions);
            bus.Subscribe<ActorDamagedEvent>(OnDamaged).AddTo(_subscriptions);
        }

        private bool Counts(int entityId) => !_world.IsRestoring && _world.ProgressOwnerOf(entityId) >= 0;

        private void OnKilled(ActorKilledEvent killed)
        {
            if (killed.ByPlayer && Counts(killed.KillerId))
                _tracker.Add(killed.KillerId, CriterionType.KillCount, killed.CharacterId ?? "");
        }

        private void OnItemAdded(ItemAddedEvent<IItem> added)
        {
            if (added.Item != null && Counts(added.EntityId))
                _tracker.Add(added.EntityId, CriterionType.ItemCollected, RolledItems.BaseOf(added.Item.ItemId), Math.Max(1, added.Quantity));
        }

        private void OnQuestCompleted(QuestCompletedEvent completed)
        {
            if (Counts(completed.EntityId))
                _tracker.Add(completed.EntityId, CriterionType.QuestCompleted, completed.QuestId ?? "");
        }

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            if (Counts(levelUp.EntityId))
                _tracker.Reach(levelUp.EntityId, CriterionType.LevelReached, "", levelUp.NewLevel);
        }

        private void OnDamaged(ActorDamagedEvent damaged)
        {
            if (damaged.Amount > 0f && damaged.AttackerId != damaged.TargetId && Counts(damaged.AttackerId))
                _tracker.Add(damaged.AttackerId, CriterionType.DamageDealt, "", (long)Math.Round(damaged.Amount));
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
