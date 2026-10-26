namespace RPGFramework.Persistence
{
    /// <summary>
    /// Represents metadata associated with a save file.
    /// </summary>
    public interface ISaveMetadata
    {
        /// <summary>
        /// Gets the timestamp when the save was created.
        /// </summary>
        long Timestamp { get; }

        /// <summary>
        /// Gets the game version that created this save.
        /// </summary>
        string GameVersion { get; }

        /// <summary>
        /// Gets the character name associated with this save.
        /// </summary>
        string CharacterName { get; }

        /// <summary>
        /// Gets the total playtime in seconds.
        /// </summary>
        float PlaytimeSeconds { get; }

        /// <summary>
        /// Gets the location name where the save was created.
        /// </summary>
        string LocationName { get; }

        /// <summary>
        /// Gets the character level at the time of save.
        /// </summary>
        int CharacterLevel { get; }

        /// <summary>
        /// Gets custom metadata as key-value pairs.
        /// </summary>
        System.Collections.Generic.Dictionary<string, string> CustomData { get; }
    }
}
