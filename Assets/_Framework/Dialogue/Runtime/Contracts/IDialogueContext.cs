using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Provides context for dialogue condition evaluation.
    /// Allows conditions to check quest status, items, stats, and custom variables.
    /// </summary>
    public interface IDialogueContext
    {
        /// <summary>
        /// The entity ID (player or NPC) engaged in dialogue.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Current node ID in the active dialogue.
        /// </summary>
        string CurrentNodeId { get; }

        /// <summary>
        /// Custom variables for branching logic (e.g., "MetVillageElder": 1).
        /// </summary>
        Dictionary<string, int> Variables { get; }

        /// <summary>
        /// Check if a quest has been completed.
        /// </summary>
        /// <param name="questId">The quest ID to check.</param>
        /// <returns>True if the quest is completed, false otherwise.</returns>
        bool HasCompletedQuest(string questId);

        /// <summary>
        /// Check if the entity owns an item.
        /// </summary>
        /// <param name="itemId">The item ID to check.</param>
        /// <returns>True if the entity owns the item, false otherwise.</returns>
        bool HasItem(string itemId);

        /// <summary>
        /// Get the current value of a stat.
        /// </summary>
        /// <param name="statId">The stat ID to retrieve (e.g., "Level", "Health", "Gold").</param>
        /// <returns>The stat value, or 0 if the stat does not exist.</returns>
        int GetStatValue(string statId);
    }
}
