using System;

namespace RPGFramework.DI
{
    /// <summary>
    /// Wrapper for lazy initialization of heavy dependencies.
    /// </summary>
    /// <typeparam name="T">The type of dependency</typeparam>
    public class LazyDependency<T> where T : class
    {
        private readonly Func<T> _factory;
        private T _value;
        private readonly object _lock = new object();
        private bool _isValueCreated;

        /// <summary>
        /// Gets the value, initializing it on first access.
        /// </summary>
        public T Value
        {
            get
            {
                if (!_isValueCreated)
                {
                    lock (_lock)
                    {
                        if (!_isValueCreated)
                        {
                            _value = _factory();
                            _isValueCreated = true;
                        }
                    }
                }
                return _value;
            }
        }

        /// <summary>
        /// Indicates whether the value has been created.
        /// </summary>
        public bool IsValueCreated => _isValueCreated;

        /// <summary>
        /// Creates a new LazyDependency instance.
        /// </summary>
        /// <param name="factory">Factory function to create the dependency</param>
        public LazyDependency(Func<T> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Explicitly creates the dependency instance if not already created.
        /// </summary>
        public T ForceInitialize()
        {
            return Value;
        }

        /// <summary>
        /// Resets the dependency instance so it will be recreated on next access.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _value = null;
                _isValueCreated = false;
            }
        }

        /// <summary>
        /// Implicitly converts LazyDependency to the dependency type.
        /// </summary>
        public static implicit operator T(LazyDependency<T> lazy)
        {
            return lazy.Value;
        }
    }
}
