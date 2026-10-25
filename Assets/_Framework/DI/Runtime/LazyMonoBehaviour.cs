using UnityEngine;

namespace RPGFramework.DI
{
    /// <summary>
    /// Base MonoBehaviour class with automatic dependency injection support.
    /// Subclasses can use [Inject] attribute on fields/properties for automatic injection.
    /// </summary>
    public abstract class LazyMonoBehaviour : MonoBehaviour
    {
        private bool _injectionPerformed = false;
        private bool _awakePerformed = false;

        /// <summary>
        /// If true, attempts automatic injection in Awake() by finding LazyDependencyInjector in scene.
        /// Override to disable automatic injection.
        /// </summary>
        protected virtual bool AutoInjectInAwake => true;

        /// <summary>
        /// Called before dependency injection.
        /// Override to perform pre-injection setup.
        /// </summary>
        protected virtual void BeforeInjection() { }

        /// <summary>
        /// Called immediately after dependency injection.
        /// Override to access injected dependencies.
        /// </summary>
        protected virtual void AfterInjection() { }

        /// <summary>
        /// Called when component is fully initialized (after Awake and injection).
        /// Override instead of Awake() to ensure dependencies are available.
        /// </summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// Alternative callback for when injection occurs.
        /// Use OnInitialized() instead for most cases.
        /// </summary>
        protected virtual void OnInject() { }

        protected virtual void Awake()
        {
            _awakePerformed = true;

            // Attempt automatic injection if enabled
            if (AutoInjectInAwake && !_injectionPerformed)
            {
                var injector = FindFirstObjectByType<LazyDependencyInjector>();
                if (injector != null)
                {
                    BeforeInjection();
                    injector.InjectDependencies(this);
                    _injectionPerformed = true;
                    AfterInjection();
                    OnInitialized();
                }
                else
                {
                    Debug.LogWarning(
                        $"LazyMonoBehaviour '{name}' requires LazyDependencyInjector for automatic injection. " +
                        $"Make sure LazyDependencyInjector exists in the scene and is initialized.", this);
                }
            }
        }

        /// <summary>
        /// Manually inject dependencies from specified container.
        /// Called automatically by LazyDependencyInjector when it scans the scene.
        /// </summary>
        public void InjectWith(IDIContainer container)
        {
            if (_injectionPerformed)
                return;

            BeforeInjection();
            container.InjectDependencies(this);
            _injectionPerformed = true;
            AfterInjection();

            if (_awakePerformed)
                OnInitialized();
        }

        /// <summary>
        /// Returns true if dependencies have been injected.
        /// </summary>
        public bool AreDependenciesInjected => _injectionPerformed;
    }
}
