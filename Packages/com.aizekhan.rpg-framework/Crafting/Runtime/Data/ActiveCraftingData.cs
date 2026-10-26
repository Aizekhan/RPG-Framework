using System;
using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// ECS component holding active crafting state for an entity.
    /// </summary>
    [Serializable]
    public struct ActiveCraftingData : IComponent
    {
        /// <summary>
        /// Dictionary of currently active crafting operations.
        /// Key: unique operation ID, Value: crafting operation state.
        /// </summary>
        public Dictionary<int, ActiveCraftingOperation> ActiveCrafts;

        /// <summary>
        /// Current crafting station access available to this entity.
        /// Maps station ID to station reference.
        /// </summary>
        public Dictionary<string, ICraftingStation> AccessibleStations;

        /// <summary>
        /// Total number of items successfully crafted by this entity.
        /// </summary>
        public int TotalItemsCrafted;

        /// <summary>
        /// Total crafting time accumulated in seconds.
        /// </summary>
        public float TotalCraftingTimeSeconds;
    }

    /// <summary>
    /// Represents a single active crafting operation.
    /// </summary>
    [Serializable]
    public struct ActiveCraftingOperation
    {
        /// <summary>
        /// Unique operation identifier.
        /// </summary>
        public int OperationId;

        /// <summary>
        /// Recipe being crafted.
        /// </summary>
        public string RecipeId;

        /// <summary>
        /// Station being used.
        /// </summary>
        public string StationId;

        /// <summary>
        /// Current state of the crafting operation.
        /// </summary>
        public CraftingState State;

        /// <summary>
        /// Time elapsed since crafting started in seconds.
        /// </summary>
        public float ElapsedTimeSeconds;

        /// <summary>
        /// Total time needed to complete in seconds.
        /// </summary>
        public float RequiredTimeSeconds;

        /// <summary>
        /// Time when the operation started.
        /// </summary>
        public DateTime StartTime;
    }
}
