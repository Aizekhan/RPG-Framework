using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Registry for managing dialogue definitions.
    /// Provides lookup and management of available dialogues.
    /// </summary>
    public interface IDialogueRegistry
    {
        /// <summary>
        /// Register a dialogue definition.
        /// </summary>
        /// <param name="dialogue">The dialogue to register.</param>
        void RegisterDialogue(IDialogue dialogue);

        /// <summary>
        /// Get a dialogue by ID.
        /// </summary>
        /// <param name="dialogueId">The ID of the dialogue to retrieve.</param>
        /// <returns>The dialogue, or null if not found.</returns>
        IDialogue? GetDialogue(string dialogueId);

        /// <summary>
        /// Get all registered dialogues.
        /// </summary>
        /// <returns>A read-only list of all registered dialogues.</returns>
        IReadOnlyList<IDialogue> GetAllDialogues();
    }
}
