namespace RPGFramework.Serialization
{
    /// <summary>
    /// Interface for data types that support versioning.
    /// Enables schema evolution and migration.
    /// </summary>
    public interface IVersionedData
    {
        /// <summary>
        /// The schema version of this data structure.
        /// </summary>
        ushort Version { get; }
    }
}
