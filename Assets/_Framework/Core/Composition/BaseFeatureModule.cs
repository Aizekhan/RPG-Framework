// Assets/_Framework/Core/Composition/BaseFeatureModule.cs
using System;
using System.Collections.Generic;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Base implementation of IFeatureModule providing common functionality.
    /// Inherit from this for concrete modules.
    /// </summary>
    public abstract class BaseFeatureModule : IFeatureModule
    {
        protected object Container { get; private set; }

        public abstract string Name { get; }

        public virtual IEnumerable<Type> Dependencies => Array.Empty<Type>();

        public void Configure(object container)
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            OnConfigure();
        }

        public virtual void Initialize()
        {
            // Override in derived classes if needed
        }

        public virtual void Shutdown()
        {
            // Override in derived classes if needed
        }

        /// <summary>
        /// Override this to register types in DI container.
        /// Container is available via protected Container property.
        /// </summary>
        protected abstract void OnConfigure();
    }
}
