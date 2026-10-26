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
        /// When true (default), <see cref="Build"/> validates the DI container after all modules are
        /// configured and fails before any module is initialized if dependencies are missing or circular.
        /// </summary>
        bool ValidateContainer { get; set; }

        /// <summary>
        /// Register a feature module.
        /// </summary>
        IGameCompositionBuilder UseModule<TModule>() where TModule : IFeatureModule, new();

        /// <summary>
        /// Register a feature module with explicit instance.
        /// </summary>
        IGameCompositionBuilder UseModule(IFeatureModule module);

        bool HasModule<TModule>() where TModule : IFeatureModule;

        /// <summary>
        /// Build the composition: resolve dependencies, configure all modules, validate the container,
        /// initialize modules. If a module fails to initialize, modules initialized before it are shut
        /// down in reverse order and the exception is rethrown.
        /// </summary>
        /// <returns>Built composition with all modules configured and initialized</returns>
        IGameComposition Build();
    }
}
