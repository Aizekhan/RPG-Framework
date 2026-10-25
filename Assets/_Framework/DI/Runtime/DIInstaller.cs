using System;

namespace RPGFramework.DI
{
    /// <summary>
    /// Base class for dependency installers.
    /// </summary>
    public abstract class DIInstaller : IDIInstaller
    {
        public abstract void InstallBindings(IDIContainer container);

        protected void BindSingleton<TService, TImplementation>(IDIContainer container)
            where TImplementation : TService
        {
            container.RegisterSingleton<TService, TImplementation>();
        }

        protected void Bind<TService, TImplementation>(IDIContainer container)
            where TImplementation : TService
        {
            container.Register<TService, TImplementation>();
        }

        protected void BindInstance<TService>(IDIContainer container, TService instance)
        {
            container.RegisterInstance<TService>(instance);
        }

        /// <summary>
        /// Executes installation with logging at the beginning and end.
        /// </summary>
        protected void InstallWithLogging(IDIContainer container, string subsystemName, Action<IDIContainer> installAction)
        {
            var logger = container.Resolve<Logging.ILogger>();
            logger.Log(Logging.LogSeverity.Information, $"Installing dependencies for {subsystemName}...", "Installer");

            installAction(container);

            logger.Log(Logging.LogSeverity.Information, $"Dependency installation for {subsystemName} completed", "Installer");
        }
    }
}
