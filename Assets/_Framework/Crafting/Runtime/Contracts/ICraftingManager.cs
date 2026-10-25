using System;
using System.Collections.Generic;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// ECS-based manager for crafting operations.
    /// Handles starting, updating, and completing crafting operations.
    /// </summary>
    public interface ICraftingManager
    {
        /// <summary>
        /// Event fired when crafting starts.
        /// </summary>
        event EventHandler<CraftingStartedEvent> CraftingStarted;

        /// <summary>
        /// Event fired when crafting completes.
        /// </summary>
        event EventHandler<CraftingCompletedEvent> CraftingCompleted;

        /// <summary>
        /// Event fired when crafting fails.
        /// </summary>
        event EventHandler<CraftingFailedEvent> CraftingFailed;

        /// <summary>
        /// Event fired when crafting is cancelled.
        /// </summary>
        event EventHandler<CraftingCancelledEvent> CraftingCancelled;

        /// <summary>
        /// Start a crafting operation.
        /// </summary>
        /// <param name="entityId">Entity performing the craft</param>
        /// <param name="recipeId">Recipe to craft</param>
        /// <param name="station">Station being used</param>
        /// <returns>Operation ID or -1 if failed</returns>
        int StartCrafting(int entityId, string recipeId, ICraftingStation station);

        /// <summary>
        /// Update active crafting operations.
        /// Typically called once per frame.
        /// </summary>
        /// <param name="deltaTimeSeconds">Time elapsed since last update</param>
        void UpdateCrafting(float deltaTimeSeconds);

        /// <summary>
        /// Cancel an active crafting operation.
        /// </summary>
        /// <param name="entityId">Entity with the operation</param>
        /// <param name="operationId">Operation ID to cancel</param>
        /// <returns>True if operation was cancelled</returns>
        bool CancelCrafting(int entityId, int operationId);

        /// <summary>
        /// Get active crafting operations for an entity.
        /// </summary>
        /// <param name="entityId">Entity to query</param>
        /// <returns>List of active operations</returns>
        IReadOnlyList<ActiveCraftingOperation> GetActiveCrafts(int entityId);

        /// <summary>
        /// Check if an entity is currently crafting.
        /// </summary>
        /// <param name="entityId">Entity to check</param>
        /// <returns>True if entity has active crafts</returns>
        bool IsCrafting(int entityId);

        /// <summary>
        /// Get the current progress of a crafting operation.
        /// </summary>
        /// <param name="entityId">Entity with the operation</param>
        /// <param name="operationId">Operation ID</param>
        /// <returns>Progress as percentage 0-100, or -1 if not found</returns>
        float GetCraftingProgress(int entityId, int operationId);
    }
}
