using RPGFramework.Loot;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Registers <see cref="ILootTableRegistry"/> and an <see cref="ILootGenerator"/> that rolls its tables.
    /// </summary>
    public sealed class LootModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Loot";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<ILootTableRegistry>(_ => new LootTableRegistry());
            Container.RegisterFactory<ILootGenerator>(c => new LootGenerator(c.Resolve<ILootTableRegistry>()));
        }
    }
}
