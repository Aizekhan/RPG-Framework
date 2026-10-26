using System;

namespace RPGFramework.DI
{
    /// <summary>
    /// Dependency injection container interface.
    /// </summary>
    public interface IDIContainer
    {
        void Register<TService, TImplementation>() where TImplementation : TService;
        void RegisterSingleton<TService, TImplementation>() where TImplementation : TService;
        void RegisterInstance<TService>(TService instance);

        /// <summary>
        /// Registers a service built by a factory that receives the container (singleton by default).
        /// </summary>
        void RegisterFactory<TService>(Func<IDIContainer, TService> factory, DILifetime lifetime = DILifetime.Singleton);
        TService Resolve<TService>();
        bool IsRegistered<TService>();

        // Methods for working with Type
        bool IsRegistered(Type type);
        object Resolve(Type type);
        void AnalyzeDependencies();

        /// <summary>
        /// Checks registrations for missing constructor dependencies and cycles without creating instances.
        /// </summary>
        DIValidationResult Validate();

        // Method for injecting dependencies into an object
        void InjectDependencies(object target);

        /// <summary>
        /// Registers a lazy singleton that will be created on first access.
        /// </summary>
        void RegisterLazySingleton<TService, TImplementation>()
            where TImplementation : TService
            where TService : class;

        /// <summary>
        /// Resolves a lazy instance of the dependency.
        /// </summary>
        LazyDependency<TService> ResolveLazy<TService>() where TService : class;

        /// <summary>
        /// Registers transient dependencies (created each time they are requested).
        /// </summary>
        void RegisterTransient<TService, TImplementation>() where TImplementation : TService;

        /// <summary>
        /// Registers scoped dependencies (exist for the lifetime of a scope).
        /// </summary>
        void RegisterScoped<TService, TImplementation>() where TImplementation : TService;

        /// <summary>
        /// Creates a new scope for dependencies.
        /// </summary>
        DIScope CreateScope(string scopeId = null);

        /// <summary>
        /// Gets the current scope or creates a new one if none exists.
        /// </summary>
        DIScope GetCurrentScope();

        /// <summary>
        /// Sets the current scope.
        /// </summary>
        void SetCurrentScope(DIScope scope);
    }
}
