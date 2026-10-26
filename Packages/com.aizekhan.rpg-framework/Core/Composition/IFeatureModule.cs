// Packages/com.aizekhan.rpg-framework/Core/Composition/IFeatureModule.cs
using System;
using System.Collections.Generic;
using RPGFramework.DI;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Base interface for all feature modules in the composition system.
    /// Modules can have dependencies on other modules and provide configuration/initialization logic.
    /// </summary>
    public interface IFeatureModule
    {
        /// <summary>
        /// Unique name identifier for this module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Other modules that this module depends on (must be loaded first).
        /// </summary>
        IEnumerable<Type> Dependencies { get; }

        /// <summary>
        /// Configure dependency injection bindings for this module.
        /// Called for every module (in dependency order) before any module is initialized.
        /// </summary>
        /// <param name="container">DI container to register types</param>
        void Configure(IDIContainer container);

        /// <summary>
        /// Initialize module after all modules are configured (and the container validated).
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shutdown and cleanup module resources.
        /// Called in reverse dependency order during application shutdown.
        /// </summary>
        void Shutdown();
    }
}
