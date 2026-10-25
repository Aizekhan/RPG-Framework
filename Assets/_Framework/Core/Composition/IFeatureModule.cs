// Assets/_Framework/Core/Composition/IFeatureModule.cs
using System;
using System.Collections.Generic;

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
        /// Called during composition building phase.
        /// </summary>
        /// <param name="container">DI container to register types</param>
        void Configure(object container);

        /// <summary>
        /// Initialize module after all dependencies are configured.
        /// Called after all modules are configured.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shutdown and cleanup module resources.
        /// Called in reverse dependency order during application shutdown.
        /// </summary>
        void Shutdown();
    }
}
