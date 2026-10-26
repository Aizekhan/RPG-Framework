// Assets/_Framework/Core/Composition/IGameComposition.cs
using System.Collections.Generic;
using RPGFramework.DI;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Represents a built game composition with all configured modules.
    /// Provides access to DI container and module management.
    /// </summary>
    public interface IGameComposition
    {
        /// <summary>
        /// DI container with all registered types from modules.
        /// </summary>
        IDIContainer Container { get; }

        /// <summary>
        /// All modules in this composition (in dependency order).
        /// </summary>
        IReadOnlyList<IFeatureModule> Modules { get; }

        bool IsShutDown { get; }

        /// <summary>
        /// Shutdown all modules in reverse dependency order. Safe to call more than once.
        /// </summary>
        void Shutdown();
    }
}
