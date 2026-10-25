// Assets/_Framework/Core/Composition/IGameCompositionBuilder.cs
using System;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Builder interface for composing game features in a modular way.
    /// Allows conditional feature registration and dependency resolution.
    /// </summary>
    public interface IGameCompositionBuilder
    {
        /// <summary>
        /// Register a feature module.
        /// </summary>
        IGameCompositionBuilder UseModule<TModule>() where TModule : IFeatureModule, new();

        /// <summary>
        /// Register a feature module with explicit instance.
        /// </summary>
        IGameCompositionBuilder UseModule(IFeatureModule module);

        /// <summary>
        /// Build the composition: resolve dependencies, configure all modules, initialize them.
        /// </summary>
        /// <returns>Built composition with all modules configured and initialized</returns>
        IGameComposition Build();
    }
}
