using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using RPGFramework.Logging;

namespace RPGFramework.DI
{
    /// <summary>
    /// DI container implementation with support for advanced features.
    /// </summary>
    public class DIContainer : IDIContainer
    {
        // Stores factories for creating instances
        private readonly Dictionary<Type, Registration> _registrations = new Dictionary<Type, Registration>();

        // Stores singleton instances
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

        // Stores LazyDependency objects for lazy initialization
        private readonly Dictionary<Type, object> _lazyDependencies = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Type> _lazyImplementations = new Dictionary<Type, Type>();

        // Logger
        private readonly ILogger _logger;

        // Current scope
        private DIScope _currentScope;
        private readonly ThreadLocal<DIScope> _threadLocalScope = new ThreadLocal<DIScope>();
        private readonly int _mainThreadId;

        // Reflection optimization - cache injection metadata
        private readonly Dictionary<Type, InjectionMetadata> _injectionCache = new Dictionary<Type, InjectionMetadata>();
        private readonly object _injectionCacheLock = new object();

        /// <summary>
        /// Cached injection metadata for performance.
        /// </summary>
        private class InjectionMetadata
        {
            public FieldInfo[] InjectableFields { get; set; }
            public PropertyInfo[] InjectableProperties { get; set; }
            public MethodInfo[] InjectableMethods { get; set; }
            public Action<object, IDIContainer> CompiledInjector { get; set; }
        }

        /// <summary>
        /// Class for storing registration data.
        /// </summary>
        private class Registration
        {
            public Type ServiceType
            {
                get;
            }
            public Type ImplementationType
            {
                get;
            }
            public DILifetime Lifetime
            {
                get;
            }
            public Func<object> Factory
            {
                get; set;
            }

            /// <summary>
            /// Created by a user factory: no constructor analysis and no member injection.
            /// </summary>
            public bool IsUserFactory
            {
                get; set;
            }

            public Registration(Type serviceType, Type implementationType, DILifetime lifetime)
            {
                ServiceType = serviceType;
                ImplementationType = implementationType;
                Lifetime = lifetime;
            }
        }

        /// <summary>
        /// Creates a new dependency container.
        /// </summary>
        public DIContainer(ILogger logger = null)
        {
            _logger = logger;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            // Register the container itself as a singleton
            RegisterInstance<IDIContainer>(this);
        }

        #region Basic Registration

        /// <summary>
        /// Registers a dependency (transient by default).
        /// </summary>
        public void Register<TService, TImplementation>() where TImplementation : TService
        {
            RegisterWithLifetime<TService, TImplementation>(DILifetime.Transient);
        }

        /// <summary>
        /// Registers a transient dependency.
        /// </summary>
        public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            RegisterWithLifetime<TService, TImplementation>(DILifetime.Transient);
        }

        /// <summary>
        /// Registers a scoped dependency.
        /// </summary>
        public void RegisterScoped<TService, TImplementation>() where TImplementation : TService
        {
            RegisterWithLifetime<TService, TImplementation>(DILifetime.Scoped);
        }

        /// <summary>
        /// Registers a singleton dependency.
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            RegisterWithLifetime<TService, TImplementation>(DILifetime.Singleton);
        }

        /// <summary>
        /// Registers a dependency with specified lifetime.
        /// </summary>
        private void RegisterWithLifetime<TService, TImplementation>(DILifetime lifetime)
            where TImplementation : TService
        {
            var serviceType = typeof(TService);
            var implementationType = typeof(TImplementation);

            var registration = new Registration(serviceType, implementationType, lifetime);
            _registrations[serviceType] = registration;

            // Create factory for the type
            registration.Factory = GetOrCreateFactory(implementationType);

            _logger?.LogDebug($"Registered {lifetime} {implementationType.Name} as {serviceType.Name}", "DI");
        }

        /// <summary>
        /// Registers a service created by a factory delegate (for types that need configuration values or
        /// non-injectable arguments). The factory receives this container to resolve dependencies.
        /// </summary>
        public void RegisterFactory<TService>(Func<IDIContainer, TService> factory, DILifetime lifetime = DILifetime.Singleton)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var serviceType = typeof(TService);
            _registrations[serviceType] = new Registration(serviceType, serviceType, lifetime)
            {
                Factory = () => factory(this),
                IsUserFactory = true
            };
            _singletons.Remove(serviceType);

            _logger?.LogDebug($"Registered {lifetime} factory for {serviceType.Name}", "DI");
        }

        /// <summary>
        /// Registers an instance as a singleton.
        /// </summary>
        public void RegisterInstance<TService>(TService instance)
        {
            var serviceType = typeof(TService);
            _singletons[serviceType] = instance;

            _logger?.LogDebug($"Registered instance of {serviceType.Name}", "DI");
        }

        /// <summary>
        /// Registers a lazy singleton that will be created on first access.
        /// </summary>
        public void RegisterLazySingleton<TService, TImplementation>()

            where TImplementation : TService
            where TService : class
        {
            var serviceType = typeof(TService);
            var implementationType = typeof(TImplementation);

            // Create factory for the type
            var factory = GetOrCreateFactory(implementationType);

            // Create LazyDependency
            var lazyDependency = new LazyDependency<TService>(() => (TService)factory());

            // Store LazyDependency
            _lazyDependencies[serviceType] = lazyDependency;
            _lazyImplementations[serviceType] = implementationType;

            _logger?.LogDebug($"Registered lazy singleton {implementationType.Name} as {serviceType.Name}", "DI");
        }

        #endregion

        #region Dependency Resolution

        /// <summary>
        /// Resolves a dependency.
        /// </summary>
        public TService Resolve<TService>()
        {
            var serviceType = typeof(TService);
            return (TService)ResolveByType(serviceType);
        }

        /// <summary>
        /// Resolves a lazy instance of the dependency.
        /// </summary>
        public LazyDependency<TService> ResolveLazy<TService>() where TService : class
        {
            var serviceType = typeof(TService);

            // Check if LazyDependency exists
            if (_lazyDependencies.TryGetValue(serviceType, out var lazyDep))
            {
                return (LazyDependency<TService>)lazyDep;
            }

            // If it doesn't exist, create a new one
            var newLazyDep = new LazyDependency<TService>(() => Resolve<TService>());
            _lazyDependencies[serviceType] = newLazyDep;

            return newLazyDep;
        }

        /// <summary>
        /// Resolves a dependency by type.
        /// </summary>
        private object ResolveByType(Type serviceType)
        {
            // Check if instance exists in singletons
            if (_singletons.TryGetValue(serviceType, out var instance))
            {
                return instance;
            }

            // Check if lazy dependency exists
            if (_lazyDependencies.TryGetValue(serviceType, out var lazyDep))
            {
                // Use reflection to call Value
                var valueProperty = lazyDep.GetType().GetProperty("Value");
                return valueProperty.GetValue(lazyDep);
            }

            // Check if type is registered
            if (!_registrations.TryGetValue(serviceType, out var registration))
            {
                throw new KeyNotFoundException($"Type {serviceType.Name} is not registered");
            }

            // Depending on lifetime
            switch (registration.Lifetime)
            {
                case DILifetime.Singleton:
                    // Create singleton if it doesn't exist yet
                    if (!_singletons.TryGetValue(serviceType, out var singleton))
                    {
                        singleton = registration.Factory();
                        _singletons[serviceType] = singleton;

                        // Inject dependencies
                        if (!registration.IsUserFactory)
                            InjectDependencies(singleton);
                    }
                    return singleton;

                case DILifetime.Scoped:
                    // Use the requested service type as the scope cache key.
                    // Create and inject the instance only once for this scope.
                    var currentScope = GetCurrentScope();
                    return currentScope.GetOrCreateInstance(serviceType, () =>
                    {
                        var scopedInstance = registration.Factory();
                        if (!registration.IsUserFactory)
                            InjectDependencies(scopedInstance);
                        return scopedInstance;
                    });

                case DILifetime.Transient:
                default:
                    // Create new instance
                    var transientInstance = registration.Factory();

                    // Inject dependencies
                    if (!registration.IsUserFactory)
                        InjectDependencies(transientInstance);

                    return transientInstance;
            }
        }

        /// <summary>
        /// Checks if type is registered.
        /// </summary>
        public bool IsRegistered<TService>()
        {
            var serviceType = typeof(TService);
            return _singletons.ContainsKey(serviceType) ||
                   _lazyDependencies.ContainsKey(serviceType) ||
                   _registrations.ContainsKey(serviceType);
        }

        public bool IsRegistered(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _singletons.ContainsKey(type) ||
                   _lazyDependencies.ContainsKey(type) ||
                   _registrations.ContainsKey(type);
        }

        public object Resolve(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // Keep generic Resolve<T>() and Resolve(Type) behavior aligned:
            // callers receive the resolved instance, not internal registration metadata.
            return ResolveByType(type);
        }

        #endregion

        #region Factories and Injection

        /// <summary>
        /// Gets or creates factory for a type.
        /// </summary>
        private Func<object> GetOrCreateFactory(Type type)
        {
            var constructor = GetPreferredConstructor(type);

            if (constructor == null)
            {
                throw new Exception($"No suitable constructor found for {type.Name}");
            }

            // Get constructor parameters
            var parameters = constructor.GetParameters();

            if (parameters.Length == 0)
            {
                // Simple case - no parameters, just create object
                return () => Activator.CreateInstance(type);
            }

            // Create parameters for lambda function
            var paramExpressions = new Expression[parameters.Length];

            // Create expressions for getting dependencies
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                // Create expression for calling ResolveByType method to get dependency
                var resolveMethod = typeof(DIContainer).GetMethod("ResolveByType",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                paramExpressions[i] = Expression.Call(
                    Expression.Constant(this),
                    resolveMethod,
                    Expression.Constant(paramType));

                // Add type conversion if needed
                if (paramType != typeof(object))
                {
                    paramExpressions[i] = Expression.Convert(paramExpressions[i], paramType);
                }
            }

            // Create expression for calling constructor
            var newExpression = Expression.New(constructor, paramExpressions);

            // Convert to object type for storage in dictionary
            var convertExpression = Expression.Convert(newExpression, typeof(object));

            // Compile expression to function
            return Expression.Lambda<Func<object>>(convertExpression).Compile();
        }

        /// <summary>
        /// Injects dependencies into an object.
        /// Optimized with reflection caching.
        /// </summary>
        public void InjectDependencies(object target)
        {
            if (target == null)
                return;

            Type targetType = target.GetType();

            // Get or create cached injection metadata
            var metadata = GetOrCreateInjectionMetadata(targetType);

            // Use compiled injector if available (fastest path)
            if (metadata.CompiledInjector != null)
            {
                metadata.CompiledInjector(target, this);
                return;
            }

            // Fallback to cached reflection (still faster than uncached)
            InjectFieldsCached(target, metadata);
            InjectPropertiesCached(target, metadata);
            InjectMethodsCached(target, metadata);
        }

        /// <summary>
        /// Get or create cached injection metadata.
        /// </summary>
        private InjectionMetadata GetOrCreateInjectionMetadata(Type targetType)
        {
            // Try fast path without lock
            if (_injectionCache.TryGetValue(targetType, out var metadata))
                return metadata;

            // Slow path with lock
            lock (_injectionCacheLock)
            {
                // Double-check after acquiring lock
                if (_injectionCache.TryGetValue(targetType, out metadata))
                    return metadata;

                // Create new metadata
                metadata = CreateInjectionMetadata(targetType);
                _injectionCache[targetType] = metadata;

                return metadata;
            }
        }

        /// <summary>
        /// Create injection metadata for a type.
        /// </summary>
        private InjectionMetadata CreateInjectionMetadata(Type targetType)
        {
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0)
                .ToArray();

            var properties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0 && p.CanWrite)
                .ToArray();

            var methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0)
                .ToArray();

            return new InjectionMetadata
            {
                InjectableFields = fields,
                InjectableProperties = properties,
                InjectableMethods = methods,
                CompiledInjector = null // Can be compiled later if needed
            };
        }

        /// <summary>
        /// Inject fields using cached metadata.
        /// </summary>
        private void InjectFieldsCached(object target, InjectionMetadata metadata)
        {
            foreach (var field in metadata.InjectableFields)
            {
                try
                {
                    var dependency = ResolveByType(field.FieldType);
                    field.SetValue(target, dependency);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error injecting field {field.Name} in {target.GetType().Name}: {ex.Message}", "DI", ex);
                }
            }
        }

        /// <summary>
        /// Inject properties using cached metadata.
        /// </summary>
        private void InjectPropertiesCached(object target, InjectionMetadata metadata)
        {
            foreach (var property in metadata.InjectableProperties)
            {
                try
                {
                    var dependency = ResolveByType(property.PropertyType);
                    property.SetValue(target, dependency);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error injecting property {property.Name} in {target.GetType().Name}: {ex.Message}", "DI", ex);
                }
            }
        }

        /// <summary>
        /// Inject methods using cached metadata.
        /// </summary>
        private void InjectMethodsCached(object target, InjectionMetadata metadata)
        {
            foreach (var method in metadata.InjectableMethods)
            {
                try
                {
                    var parameters = method.GetParameters();
                    var args = new object[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = ResolveByType(parameters[i].ParameterType);
                    }

                    method.Invoke(target, args);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error injecting method {method.Name} in {target.GetType().Name}: {ex.Message}", "DI", ex);
                }
            }
        }

        #endregion

        #region Scope Management

        /// <summary>
        /// Creates a new scope for dependencies.
        /// </summary>
        public DIScope CreateScope(string scopeId = null)
        {
            return new DIScope(this, _currentScope, scopeId);
        }

        /// <summary>
        /// Gets the current scope or creates a new one if none exists.
        /// </summary>

        public DIScope GetCurrentScope()
        {
            // First check thread-local scope
            var threadScope = _threadLocalScope?.Value;
            if (threadScope != null)
                return threadScope;

            // If no scope exists, create a new one
            if (_currentScope == null)
            {
                _currentScope = CreateScope("default");
                _logger?.LogInfo("Created default DI scope", "DI");
            }

            return _currentScope;
        }

        /// <summary>
        /// Sets the current scope.
        /// </summary>
        public void SetCurrentScope(DIScope scope)
        {
            if (scope == null)
            {
                _logger?.LogWarning("Attempted to set null scope", "DI");
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                // Keep Unity/main-thread scope changes in the container's main scope.
                _currentScope = scope;
            }
            else
            {
                // Other threads get an isolated scope; do not overwrite the main scope.
                _threadLocalScope.Value = scope;
            }

            _logger?.LogDebug($"Set current DI scope to {scope.ScopeId}", "DI");
        }

        #endregion

        #region Analysis and Diagnostics

        /// <summary>
        /// Analyzes dependencies between types.
        /// </summary>
        public void AnalyzeDependencies()
        {
            if (_logger == null)
                return;

            _logger.LogInfo("Analyzing dependencies...", "DI");

            // List of all registered types
            var allTypes = new HashSet<Type>();

            // Add singletons
            foreach (var singleton in _singletons.Keys)
                allTypes.Add(singleton);

            // Add types from registrations
            foreach (var registration in _registrations.Values)
            {
                if (registration.IsUserFactory)
                    continue;
                allTypes.Add(registration.ServiceType);
                allTypes.Add(registration.ImplementationType);
            }

            // Analyze dependencies for each type
            foreach (var type in allTypes)
            {
                var ctor = GetPreferredConstructor(type);
                if (ctor == null)
                    continue;

                var parameters = ctor.GetParameters();
                if (parameters.Length == 0)
                    continue;

                var dependencyList = new List<string>();
                foreach (var param in parameters)
                {
                    var paramType = param.ParameterType;
                    var isRegistered = _singletons.ContainsKey(paramType) || _registrations.ContainsKey(paramType);
                    dependencyList.Add($"{paramType.Name} (Registered: {isRegistered})");
                }

                _logger.LogInfo($"Type {type.Name} depends on: {string.Join(", ", dependencyList)}", "DI");
            }
        }

        /// <summary>
        /// Constructor used both for instantiation and for validation:
        /// the one marked [Inject], otherwise the first public constructor.
        /// </summary>
        private static ConstructorInfo GetPreferredConstructor(Type type)
        {
            var constructors = type.GetConstructors();
            return constructors.FirstOrDefault(c => c.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0) ??
                (constructors.Length > 0 ? constructors[0] : null);
        }

        /// <summary>
        /// Checks if a dependency is missing.
        /// </summary>
        public bool ValidateMissingDependency<TService>()
        {
            var serviceType = typeof(TService);
            return _singletons.ContainsKey(serviceType) || _registrations.ContainsKey(serviceType);
        }

        /// <summary>
        /// Validates all dependencies for a type.
        /// </summary>
        public void ValidateDependencies(Type type)
        {
            var ctor = GetPreferredConstructor(type);
            if (ctor == null)
                return;

            var parameters = ctor.GetParameters();
            foreach (var param in parameters)
            {
                var paramType = param.ParameterType;
                if (!_singletons.ContainsKey(paramType) && !_registrations.ContainsKey(paramType))
                {
                    _logger?.LogWarning($"Missing dependency: {paramType.Name} for {type.Name}", "DI");
                }
            }
        }

        /// <summary>
        /// Validates all registered dependencies.
        /// </summary>
        public void ValidateAllDependencies()
        {
            if (_logger == null)
                return;

            _logger.LogInfo("Validating all dependencies...", "DI");

            // List of all registered implementation types
            var allTypes = new HashSet<Type>();
            foreach (var registration in _registrations.Values)
            {
                if (!registration.IsUserFactory)
                    allTypes.Add(registration.ImplementationType);
            }

            // Validate each type
            foreach (var type in allTypes)
            {
                ValidateDependencies(type);
            }

            _logger.LogInfo("Dependency validation completed", "DI");
        }

        /// <summary>
        /// Checks every type-based registration without creating instances:
        /// constructor parameters that are not registered, types without a public constructor,
        /// and circular constructor dependencies.
        /// </summary>
        public DIValidationResult Validate()
        {
            var implementations = new Dictionary<Type, Type>();
            foreach (var registration in _registrations.Values)
            {
                if (!registration.IsUserFactory)
                    implementations[registration.ServiceType] = registration.ImplementationType;
            }
            foreach (var pair in _lazyImplementations)
                implementations[pair.Key] = pair.Value;

            var issues = new List<DIValidationIssue>();
            var edges = new Dictionary<Type, List<Type>>();

            foreach (var pair in implementations)
            {
                var constructor = GetPreferredConstructor(pair.Value);
                if (constructor == null)
                {
                    issues.Add(new DIValidationIssue(DIValidationIssueKind.NoPublicConstructor, pair.Key, pair.Value, null,
                        $"{pair.Value.Name} (registered as {pair.Key.Name}) has no public constructor"));
                    continue;
                }

                var dependencies = new List<Type>();
                foreach (var parameter in constructor.GetParameters())
                {
                    var dependency = parameter.ParameterType;
                    if (IsRegistered(dependency))
                    {
                        if (implementations.ContainsKey(dependency))
                            dependencies.Add(dependency);
                    }
                    else
                    {
                        issues.Add(new DIValidationIssue(DIValidationIssueKind.MissingDependency, pair.Key, pair.Value, dependency,
                            $"{pair.Value.Name} requires {dependency.Name}, which is not registered"));
                    }
                }
                edges[pair.Key] = dependencies;
            }

            // Depth-first search for cycles (0 = unvisited, 1 = on stack, 2 = done)
            var state = new Dictionary<Type, int>();
            var stack = new List<Type>();
            foreach (var service in edges.Keys)
                FindCycles(service, edges, state, stack, issues);

            return new DIValidationResult(issues);
        }

        private static void FindCycles(Type service, Dictionary<Type, List<Type>> edges, Dictionary<Type, int> state,
            List<Type> stack, List<DIValidationIssue> issues)
        {
            state.TryGetValue(service, out int current);
            if (current == 2)
                return;
            if (current == 1)
            {
                var cycle = stack.Skip(stack.IndexOf(service)).Concat(new[] { service }).Select(t => t.Name);
                issues.Add(new DIValidationIssue(DIValidationIssueKind.CircularDependency, service, null, null,
                    "Circular dependency: " + string.Join(" -> ", cycle)));
                return;
            }

            state[service] = 1;
            stack.Add(service);
            if (edges.TryGetValue(service, out var dependencies))
            {
                foreach (var dependency in dependencies)
                    FindCycles(dependency, edges, state, stack, issues);
            }
            stack.RemoveAt(stack.Count - 1);
            state[service] = 2;
        }

        #endregion
    }
}
