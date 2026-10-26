using RPGFramework.Items;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Registers the <see cref="IItemRegistry"/> the game fills with its item definitions.
    /// </summary>
    public sealed class ItemsModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Items";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<IItemRegistry>(_ => new ItemRegistry());
        }
    }
}
