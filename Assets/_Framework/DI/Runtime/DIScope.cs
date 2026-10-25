using System;
using System.Collections.Generic;

namespace RPGFramework.DI
{
    /// <summary>
    /// Dependency lifecycle type.
    /// </summary>
    public enum DILifetime
    {
        /// <summary>
        /// Transient dependencies are created every time they are requested.
        /// </summary>
        Transient,

        /// <summary>
        /// Scoped dependencies exist for the lifetime of a scope.
        /// </summary>
        Scoped,

        /// <summary>
        /// Singletons exist for the entire lifetime of the application.
        /// </summary>
        Singleton
    }

    /// <summary>
    /// Scope for managing dependency lifetimes.
    /// </summary>
    public class DIScope : IDisposable
    {
        private readonly IDIContainer _container;
        private readonly Dictionary<Type, object> _scopedInstances = new Dictionary<Type, object>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly DIScope _parentScope;
        private readonly object _syncLock = new object();
        private bool _isDisposed;

        /// <summary>
        /// Scope identifier.
        /// </summary>
        public string ScopeId
        {
            get;
        }

        /// <summary>
        /// Parent scope.
        /// </summary>
        public DIScope ParentScope => _parentScope;

        /// <summary>
        /// Creates a new scope.
        /// </summary>
        /// <param name="container">DI container</param>
        /// <param name="parentScope">Parent scope, if any</param>
        /// <param name="scopeId">Scope identifier</param>
        public DIScope(IDIContainer container, DIScope parentScope = null, string scopeId = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _parentScope = parentScope;
            ScopeId = scopeId ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Gets or creates a dependency instance in the current scope.
        /// </summary>
        public TService GetOrCreateInstance<TService>() where TService : class
        {
            return (TService)GetOrCreateInstance(typeof(TService), () => _container.Resolve<TService>());
        }

        // The container supplies the factory so scoped resolution can be cached under
        // the actual requested service type without recursively resolving object.
        internal object GetOrCreateInstance(Type serviceType, Func<object> factory)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (_syncLock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(ScopeId);

                if (_scopedInstances.TryGetValue(serviceType, out var instance))
                    return instance;

                var newInstance = factory();

                if (newInstance is IDisposable disposable && !_disposables.Contains(disposable))
                    _disposables.Add(disposable);

                _scopedInstances[serviceType] = newInstance;
                return newInstance;
            }
        }

        /// <summary>
        /// Checks if a dependency instance has already been created in this scope.
        /// </summary>
        public bool HasInstance<TService>()
        {
            lock (_syncLock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(ScopeId);

                return _scopedInstances.ContainsKey(typeof(TService));
            }
        }

        /// <summary>
        /// Releases scope resources.
        /// </summary>
        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_isDisposed)
                    return;

                // Dispose in reverse creation order (stack semantics). Continue even if
                // one resource throws, so all other scope-owned resources are released.
                for (var i = _disposables.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        _disposables[i].Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing instance: {ex.Message}");
                    }
                }

                _disposables.Clear();
                _scopedInstances.Clear();
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Creates a child scope.
        /// </summary>
        public DIScope CreateChildScope(string scopeId = null)
        {
            lock (_syncLock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(ScopeId);

                return new DIScope(_container, this, scopeId);
            }
        }
    }
}
