namespace RPGFramework.DI
{
    /// <summary>
    /// Interface for dependency installers.
    /// </summary>
    public interface IDIInstaller
    {
        void InstallBindings(IDIContainer container);
    }
}
