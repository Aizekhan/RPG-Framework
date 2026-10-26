using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Manages active dialogues, state progression, and dialogue operations.
    /// Supports multiple concurrent dialogues per entity.
    /// </summary>
    public interface IDialogueManager
    {
        /// <summary>
        /// Start a new dialogue for an entity.
        /// </summary>
        /// <param name="entityId">The entity starting the dialogue.</param>
        /// <param name="dialogueId">The ID of the dialogue to start.</param>
        /// <returns>True if the dialogue started successfully, false otherwise.</returns>
        bool StartDialogue(int entityId, string dialogueId);

        /// <summary>
        /// Advance the dialogue to the next node.
        /// </summary>
        /// <param name="entityId">The entity advancing the dialogue.</param>
        /// <param name="choiceId">Optional ID of the choice to select. If null, progresses to the first next node.</param>
        /// <returns>True if the dialogue advanced successfully, false if dialogue ended or error occurred.</returns>
        bool AdvanceDialogue(int entityId, string? choiceId = null);

        /// <summary>
        /// End a dialogue for an entity.
        /// </summary>
        /// <param name="entityId">The entity ending the dialogue.</param>
        void EndDialogue(int entityId);

        /// <summary>
        /// Get the current progress of a dialogue for an entity.
        /// </summary>
        /// <param name="entityId">The entity to check.</param>
        /// <returns>The dialogue progress, or null if no dialogue is active.</returns>
        IDialogueProgress? GetDialogueProgress(int entityId);

        /// <summary>
        /// Get the current node being displayed in a dialogue.
        /// </summary>
        /// <param name="entityId">The entity in dialogue.</param>
        /// <returns>The current dialogue node, or null if no dialogue is active.</returns>
        IDialogueNode? GetCurrentNode(int entityId);

        /// <summary>
        /// Get the available choices for the current node.
        /// </summary>
        /// <param name="entityId">The entity in dialogue.</param>
        /// <returns>List of available choices, or empty list if no dialogue is active or current node has no choices.</returns>
        IReadOnlyList<IDialogueChoice> GetAvailableChoices(int entityId);
    }
}
