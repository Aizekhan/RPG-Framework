namespace RPGFramework.Serialization
{
    /// <summary>
    /// Registry for schema migration between versions.
    /// Maps (typeId, fromVersion, toVersion) to migration logic.
    /// </summary>
    public interface IMigrationRegistry
    {
        /// <summary>
        /// Registers a migration from one version to the next.
        /// </summary>
        void RegisterMigration<TFrom, TTo>(string typeId, ushort fromVersion, ushort toVersion, IMigrator<TFrom, TTo> migrator)
            where TFrom : ISerializable
            where TTo : ISerializable;

        /// <summary>
        /// Migrates data from one version to another.
        /// </summary>
        byte[] Migrate(string typeId, byte[] data, ushort fromVersion, ushort toVersion);

        /// <summary>
        /// Checks if a migration path exists.
        /// </summary>
        bool HasMigration(string typeId, ushort fromVersion, ushort toVersion);
    }
}
