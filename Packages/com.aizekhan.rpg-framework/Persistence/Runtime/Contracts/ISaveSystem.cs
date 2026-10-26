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
        /// Opens the save in the specified slot (makes it active and returns its metadata) without reading the game
        /// state: <see cref="LoadResult.GameState"/> is null. Use <see cref="Load{TState}"/> to get the state.
        /// </summary>
        LoadResult Load(string slotId);

        /// <summary>
        /// Loads the game state saved in the specified slot as <typeparamref name="TState"/>.
        /// </summary>
        LoadResult Load<TState>(string slotId);

        /// <summary>
        /// Loads the save that the latest one replaced (kept on every save while it was intact), e.g. when the latest
        /// cannot be used. Fails with <see cref="SaveError.FileNotFound"/> when there is none.
        /// </summary>
        LoadResult LoadPrevious<TState>(string slotId);

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
        /// Opens the quick save without reading the game state (see <see cref="Load(string)"/>).
        /// </summary>
        LoadResult QuickLoad();

        /// <summary>
        /// Loads the quick save's game state as <typeparamref name="TState"/>.
        /// </summary>
        LoadResult QuickLoad<TState>();

        /// <summary>
        /// Triggers an auto-save if conditions are met.
        /// </summary>
        void TriggerAutoSave(object gameState, ISaveMetadata metadata);
    }
}
