namespace RPGFramework.Serialization
{
    /// <summary>
    /// Interface for schema migration between versions.
    /// Implement this to support backward compatibility when schema changes.
    /// </summary>
    /// <typeparam name="TFrom">Source version type</typeparam>
    /// <typeparam name="TTo">Target version type</typeparam>
    public interface IMigrator<TFrom, TTo>
    {
        /// <summary>
        /// Migrates data from old version to new version.
        /// </summary>
        TTo Migrate(TFrom oldVersion);
    }
}
