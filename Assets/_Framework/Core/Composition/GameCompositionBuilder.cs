// Assets/_Framework/Core/Composition/GameCompositionBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Core.Initialization;
using RPGFramework.DI;
using RPGFramework.Logging;

namespace RPGFramework.Core.Composition
{
    /// <summary>
    /// Implementation of modular composition builder.
    /// Handles module registration, dependency resolution, and initialization.
    /// </summary>
    public class GameCompositionBuilder : IGameCompositionBuilder
    {
        private const string Category = "Composition";

        private readonly IDIContainer _container;
        private readonly ILogger _logger;
        private readonly List<IFeatureModule> _modules = new List<IFeatureModule>();
        private readonly HashSet<Type> _registeredModuleTypes = new HashSet<Type>();
        private bool _built;

        public GameCompositionBuilder(IDIContainer container, ILogger logger = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _logger = logger;
        }

        public bool ValidateContainer { get; set; } = true;

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
                _logger.LogDebug($"Module {module.Name} is already registered, skipping", Category);
                return this;
            }

            _modules.Add(module);
            _registeredModuleTypes.Add(moduleType);

            return this;
        }

        public bool HasModule<TModule>() where TModule : IFeatureModule =>
            _modules.Any(m => m is TModule);

        public IGameComposition Build()
        {
            if (_built)
                throw new InvalidOperationException("Build() can only be called once per builder");
            _built = true;

            // 1. Resolve dependencies (topological sort)
            var sortedModules = ResolveDependencies(_modules);

            // 2. Configure all modules
            foreach (var module in sortedModules)
            {
                module.Configure(_container);
            }

            // 3. Fail fast on a broken object graph, before anything is initialized
            if (ValidateContainer)
                _container.Validate().ThrowIfInvalid();

            // 4. Initialize all modules; on failure shut down the ones already initialized
            using (var transaction = new InitializationTransaction(_logger))
            {
                foreach (var module in sortedModules)
                {
                    module.Initialize();
                    transaction.RegisterRollback(module.Shutdown, $"shutdown {module.Name}");
                }

                transaction.Commit();
            }

            _logger.LogInfo($"Composition built: {string.Join(", ", sortedModules.Select(m => m.Name))}", Category);
            return new GameComposition(_container, sortedModules, _logger);
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
            private readonly ILogger _logger;

            public IDIContainer Container { get; }
            public IReadOnlyList<IFeatureModule> Modules { get; }
            public bool IsShutDown { get; private set; }

            public GameComposition(IDIContainer container, List<IFeatureModule> modules, ILogger logger)
            {
                Container = container;
                Modules = modules.AsReadOnly();
                _logger = logger;
            }

            public void Shutdown()
            {
                if (IsShutDown)
                    return;
                IsShutDown = true;

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
                        _logger.LogError($"Error shutting down module '{Modules[i].Name}'", Category, ex);
                    }
                }
            }
        }
    }
}
