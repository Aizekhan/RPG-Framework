using System.Collections.Generic;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Core save system interface for managing game saves.
    /// </summary>
    public interface ISaveSystem
    {
        /// <summary>
        /// Gets all available save slots.
        /// </summary>
        IReadOnlyList<ISaveSlot> Slots { get; }

        /// <summary>
        /// Gets the currently active save slot, if any.
        /// </summary>
        ISaveSlot ActiveSlot { get; }

        /// <summary>
        /// Gets the maximum number of save slots allowed.
        /// </summary>
        int MaxSlots { get; }

        /// <summary>
        /// Gets whether auto-save is enabled.
        /// </summary>
        bool AutoSaveEnabled { get; set; }

        /// <summary>
        /// Gets the auto-save interval in seconds.
        /// </summary>
        float AutoSaveIntervalSeconds { get; set; }

        /// <summary>
        /// Saves the current game state to the specified slot.
        /// </summary>
        SaveResult Save(string slotId, object gameState, ISaveMetadata metadata);

        /// <summary>
        /// Loads game state from the specified slot.
        /// </summary>
        LoadResult Load(string slotId);

        /// <summary>
        /// Deletes the save in the specified slot.
        /// </summary>
        bool DeleteSave(string slotId);

        /// <summary>
        /// Gets the metadata for a save slot without loading the full save.
        /// </summary>
        ISaveMetadata GetMetadata(string slotId);

        /// <summary>
        /// Checks if a slot exists and is occupied.
        /// </summary>
        bool HasSave(string slotId);

        /// <summary>
        /// Creates a quick save (auto-save functionality).
        /// </summary>
        SaveResult QuickSave(object gameState, ISaveMetadata metadata);

        /// <summary>
        /// Loads the most recent quick save.
        /// </summary>
        LoadResult QuickLoad();

        /// <summary>
        /// Triggers an auto-save if conditions are met.
        /// </summary>
        void TriggerAutoSave(object gameState, ISaveMetadata metadata);
    }
}
