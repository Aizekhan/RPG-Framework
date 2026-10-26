using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default ECS-based implementation of ICraftingManager.
    /// Manages active crafting operations across all entities.
    /// </summary>
    public class CraftingManager : ICraftingManager
    {
        private readonly IEntityManager _entityManager;
        private readonly ICraftingRecipeRegistry _recipeRegistry;
        private int _nextOperationId = 1;

        /// <inheritdoc/>
        public event EventHandler<CraftingStartedEvent> CraftingStarted;

        /// <inheritdoc/>
        public event EventHandler<CraftingCompletedEvent> CraftingCompleted;

        /// <inheritdoc/>
        public event EventHandler<CraftingFailedEvent> CraftingFailed;

        /// <inheritdoc/>
        public event EventHandler<CraftingCancelledEvent> CraftingCancelled;

        /// <summary>
        /// Create a new crafting manager.
        /// </summary>
        public CraftingManager(IEntityManager entityManager, ICraftingRecipeRegistry recipeRegistry)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _recipeRegistry = recipeRegistry ?? throw new ArgumentNullException(nameof(recipeRegistry));
        }

        /// <inheritdoc/>
        public int StartCrafting(int entityId, string recipeId, ICraftingStation station)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (station == null)
                throw new ArgumentNullException(nameof(station));

            var recipe = _recipeRegistry.GetRecipe(recipeId);
            if (recipe == null)
                return -1;

            if (!station.CanCraft(recipe))
                return -1;

            if (!recipe.CanCraft(entityId))
                return -1;

            if (!station.IsAvailable())
                return -1;

            // Get or create active crafting component
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            if (activeCrafts.ActiveCrafts == null)
            {
                activeCrafts = new ActiveCraftingData
                {
                    ActiveCrafts = new Dictionary<int, ActiveCraftingOperation>(),
                    AccessibleStations = new Dictionary<string, ICraftingStation>(),
                    TotalItemsCrafted = 0,
                    TotalCraftingTimeSeconds = 0
                };
            }

            var operationId = _nextOperationId++;
            var operation = new ActiveCraftingOperation
            {
                OperationId = operationId,
                RecipeId = recipeId,
                StationId = station.StationId,
                State = CraftingState.InProgress,
                ElapsedTimeSeconds = 0,
                RequiredTimeSeconds = recipe.CraftingTimeSeconds,
                StartTime = DateTime.UtcNow
            };

            activeCrafts.ActiveCrafts[operationId] = operation;

            _entityManager.RemoveComponent<ActiveCraftingData>(entityId);
            _entityManager.AddComponent(entityId, activeCrafts);

            CraftingStarted?.Invoke(this, new CraftingStartedEvent(entityId, recipeId, station, recipe.CraftingTimeSeconds));

            return operationId;
        }

        /// <inheritdoc/>
        public void UpdateCrafting(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds < 0)
                throw new ArgumentException("Delta time cannot be negative", nameof(deltaTimeSeconds));

            // This would iterate through all entities with ActiveCraftingData
            // and update their crafting progress
            // Since we don't have direct access to all entities, this is a simplified version
        }

        /// <inheritdoc/>
        public bool CancelCrafting(int entityId, int operationId)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            if (activeCrafts.ActiveCrafts == null || !activeCrafts.ActiveCrafts.TryGetValue(operationId, out var operation))
                return false;

            var timeSpent = operation.ElapsedTimeSeconds;
            var recipeId = operation.RecipeId;

            activeCrafts.ActiveCrafts.Remove(operationId);
            _entityManager.RemoveComponent<ActiveCraftingData>(entityId);
            _entityManager.AddComponent(entityId, activeCrafts);

            CraftingCancelled?.Invoke(this, new CraftingCancelledEvent(entityId, recipeId, timeSpent));

            return true;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActiveCraftingOperation> GetActiveCrafts(int entityId)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            return activeCrafts.ActiveCrafts?.Values.ToList() ?? new List<ActiveCraftingOperation>();
        }

        /// <inheritdoc/>
        public bool IsCrafting(int entityId)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            return activeCrafts.ActiveCrafts != null && activeCrafts.ActiveCrafts.Count > 0;
        }

        /// <inheritdoc/>
        public float GetCraftingProgress(int entityId, int operationId)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            if (activeCrafts.ActiveCrafts == null || !activeCrafts.ActiveCrafts.TryGetValue(operationId, out var operation))
                return -1;

            if (operation.RequiredTimeSeconds == 0)
                return 100f;

            return Math.Min(100f, (operation.ElapsedTimeSeconds / operation.RequiredTimeSeconds) * 100f);
        }

        /// <summary>
        /// Complete a crafting operation and produce results.
        /// </summary>
        internal void CompleteCrafting(int entityId, int operationId, IEnumerable<ICraftingResult> results)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            if (activeCrafts.ActiveCrafts == null || !activeCrafts.ActiveCrafts.TryGetValue(operationId, out var operation))
                return;

            activeCrafts.ActiveCrafts.Remove(operationId);
            activeCrafts.TotalItemsCrafted += results.Sum(r => r.Quantity);
            activeCrafts.TotalCraftingTimeSeconds += operation.ElapsedTimeSeconds;

            _entityManager.RemoveComponent<ActiveCraftingData>(entityId);
            _entityManager.AddComponent(entityId, activeCrafts);

            CraftingCompleted?.Invoke(this, new CraftingCompletedEvent(
                entityId,
                operation.RecipeId,
                results,
                operation.ElapsedTimeSeconds));
        }

        /// <summary>
        /// Fail a crafting operation.
        /// </summary>
        internal void FailCrafting(int entityId, int operationId, string failureReason)
        {
            var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
            if (activeCrafts.ActiveCrafts == null || !activeCrafts.ActiveCrafts.TryGetValue(operationId, out var operation))
                return;

            activeCrafts.ActiveCrafts.Remove(operationId);
            _entityManager.RemoveComponent<ActiveCraftingData>(entityId);
            _entityManager.AddComponent(entityId, activeCrafts);

            CraftingFailed?.Invoke(this, new CraftingFailedEvent(
                entityId,
                operation.RecipeId,
                failureReason,
                operation.ElapsedTimeSeconds));
        }
    }
}
