// Assets/_Framework/Core/Composition/Modules/FrameworkCoreModule.cs
using RPGFramework.ECS;
using RPGFramework.Logging;

namespace RPGFramework.Core.Composition.Modules
{
    /// <summary>
    /// Framework Core module: universal ECS runtime, DI, and logging.
    /// This module has zero game-specific dependencies and can be reused across any RPG game.
    ///
    /// NOTE: Framework Core services are registered by FrameworkCoreInstaller,
    /// which is called from GameLayerModule due to assembly dependency constraints.
    /// FrameworkCoreInstaller registers: IEntityManager, IEcsWorld, IEventPool,
    /// IDILifecycleManager, ISystemJobScheduler, and ParallelSystemRegistry.
    /// </summary>
    public class FrameworkCoreModule : BaseFeatureModule
    {
        public override string Name => "Framework.Core";

        protected override void OnConfigure()
        {
            // Framework Core services are registered by FrameworkCoreInstaller
            // called from GameLayerModule (cannot call directly due to assembly constraints)
        }

        public override void Initialize()
        {
            // Framework core has no initialization logic
            // Entities and systems are initialized by EcsWorld (Game Layer responsibility)
        }
    }
}
