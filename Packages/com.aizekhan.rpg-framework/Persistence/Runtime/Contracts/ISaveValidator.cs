namespace RPGFramework.Persistence
{
    /// <summary>
    /// Validates save file integrity and compatibility.
    /// </summary>
    public interface ISaveValidator
    {
        /// <summary>
        /// Validates a save file's integrity.
        /// </summary>
        bool ValidateIntegrity(ISaveFile saveFile);

        /// <summary>
        /// Checks if a save file is compatible with the current game version.
        /// </summary>
        bool IsCompatible(ISaveFile saveFile);

        /// <summary>
        /// Attempts to repair a corrupted save file.
        /// </summary>
        bool TryRepair(ISaveFile saveFile, out ISaveFile repairedFile);

        /// <summary>
        /// Computes a checksum for the given data.
        /// </summary>
        string ComputeChecksum(byte[] data);
    }
}
