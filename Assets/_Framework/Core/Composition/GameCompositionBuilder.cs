// Assets/_Framework/Core/Composition/GameCompositionBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Implementation of modular composition builder.
    /// Handles module registration, dependency resolution, and initialization.
    /// </summary>
    public class GameCompositionBuilder : IGameCompositionBuilder
    {
        private readonly object _container;
        private readonly List<IFeatureModule> _modules = new List<IFeatureModule>();
        private readonly HashSet<Type> _registeredModuleTypes = new HashSet<Type>();

        public GameCompositionBuilder(object container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public IGameCompositionBuilder UseModule<TModule>() where TModule : IFeatureModule, new()
        {
            return UseModule(new TModule());
        }

        public IGameCompositionBuilder UseModule(IFeatureModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            var moduleType = module.GetType();
            if (_registeredModuleTypes.Contains(moduleType))
            {
                // Module already registered, skip
                return this;
            }

            _modules.Add(module);
            _registeredModuleTypes.Add(moduleType);

            return this;
        }

        public IGameComposition Build()
        {
            // 1. Resolve dependencies (topological sort)
            var sortedModules = ResolveDependencies(_modules);

            // 2. Configure all modules
            foreach (var module in sortedModules)
            {
                module.Configure(_container);
            }

            // 3. Initialize all modules
            foreach (var module in sortedModules)
            {
                module.Initialize();
            }

            return new GameComposition(_container, sortedModules);
        }

        private List<IFeatureModule> ResolveDependencies(List<IFeatureModule> modules)
        {
            var sorted = new List<IFeatureModule>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();

            foreach (var module in modules)
            {
                Visit(module, modules, sorted, visited, visiting);
            }

            return sorted;
        }

        private void Visit(
            IFeatureModule module,
            List<IFeatureModule> allModules,
            List<IFeatureModule> sorted,
            HashSet<Type> visited,
            HashSet<Type> visiting)
        {
            var moduleType = module.GetType();

            if (visited.Contains(moduleType))
                return;

            if (visiting.Contains(moduleType))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected for module: {module.Name}");
            }

            visiting.Add(moduleType);

            // Visit dependencies first
            foreach (var dependencyType in module.Dependencies)
            {
                var dependencyModule = allModules.FirstOrDefault(m => dependencyType.IsInstanceOfType(m));

                if (dependencyModule == null)
                {
                    throw new InvalidOperationException(
                        $"Module '{module.Name}' depends on '{dependencyType.Name}', but it's not registered. " +
                        $"Register the dependency module before '{module.Name}'.");
                }

                Visit(dependencyModule, allModules, sorted, visited, visiting);
            }

            visiting.Remove(moduleType);
            visited.Add(moduleType);
            sorted.Add(module);
        }

        private class GameComposition : IGameComposition
        {
            public object Container { get; }
            public IReadOnlyList<IFeatureModule> Modules { get; }

            public GameComposition(object container, List<IFeatureModule> modules)
            {
                Container = container;
                Modules = modules.AsReadOnly();
            }

            public void Shutdown()
            {
                // Shutdown in reverse order
                for (int i = Modules.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Modules[i].Shutdown();
                    }
                    catch (Exception ex)
                    {
                        // Log but continue shutting down other modules
                        UnityEngine.Debug.LogError(
                            $"Error shutting down module '{Modules[i].Name}': {ex.Message}");
                    }
                }
            }
        }
    }
}
