using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Quests;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Registers <see cref="IQuestRegistry"/> and <see cref="IQuestManager"/> (quest progress lives on entities of the
    /// core <see cref="IEntityManager"/>; quest events go to the event bus).
    /// </summary>
    public sealed class QuestsModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Quests";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<IQuestRegistry>(_ => new QuestRegistry());
            Container.RegisterFactory<IQuestManager>(c =>
                new QuestManager(c.Resolve<IEntityManager>(), c.Resolve<IQuestRegistry>(), c.Resolve<IEventPublisher>()));
        }
    }
}
