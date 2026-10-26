namespace RPGFramework.DI
{
    /// <summary>
    /// Interface for dependency injectors.
    /// Provides methods for injecting dependencies into objects.
    /// </summary>
    public interface IDependencyInjector
    {
        /// <summary>
        /// Initializes the injector with a DI container.
        /// </summary>
        /// <param name="container">The dependency injection container</param>
        void Initialize(IDIContainer container);

        /// <summary>
        /// Scans the current scene and injects dependencies into all LazyMonoBehaviour components.
        /// </summary>
        void ScanAndInjectInScene();

        /// <summary>
        /// Injects dependencies into a specific object instance.
        /// </summary>
        /// <param name="instance">The object to inject dependencies into</param>
        void InjectDependencies(object instance);

        /// <summary>
        /// Injects dependencies into all MonoBehaviour components in the scene.
        /// Alias for ScanAndInjectInScene for clarity.
        /// </summary>
        void InjectDependenciesInScene();
    }
}
