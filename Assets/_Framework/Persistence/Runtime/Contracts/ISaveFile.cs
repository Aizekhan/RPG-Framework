namespace RPGFramework.Persistence
{
    /// <summary>
    /// Represents a save file containing game state data.
    /// </summary>
    public interface ISaveFile
    {
        /// <summary>
        /// Gets the metadata for this save file.
        /// </summary>
        ISaveMetadata Metadata { get; }

        /// <summary>
        /// Gets the serialized game state data.
        /// </summary>
        byte[] Data { get; }

        /// <summary>
        /// Gets the checksum for data integrity validation.
        /// </summary>
        string Checksum { get; }

        /// <summary>
        /// Validates the integrity of this save file.
        /// </summary>
        bool ValidateIntegrity();
    }
}
