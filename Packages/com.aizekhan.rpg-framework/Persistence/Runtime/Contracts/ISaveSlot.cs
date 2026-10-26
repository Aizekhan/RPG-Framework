namespace RPGFramework.Persistence
{
    /// <summary>
    /// Represents a save slot that can contain a save file.
    /// </summary>
    public interface ISaveSlot
    {
        /// <summary>
        /// Gets the unique identifier for this save slot.
        /// </summary>
        string SlotId { get; }

        /// <summary>
        /// Gets whether this slot currently contains a save file.
        /// </summary>
        bool IsOccupied { get; }

        /// <summary>
        /// Gets the metadata for the save in this slot, if any.
        /// </summary>
        ISaveMetadata Metadata { get; }

        /// <summary>
        /// Gets the file path for this save slot.
        /// </summary>
        string FilePath { get; }
    }
}
