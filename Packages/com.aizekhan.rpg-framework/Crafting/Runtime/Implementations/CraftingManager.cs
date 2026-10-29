using RPGFramework.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default ECS-based implementation of ICraftingManager.
    /// Manages active crafting operations across all entities: <see cref="UpdateCrafting"/> moves every craft on, and
    /// a craft whose time is up completes with the results that came out (each by its success chance), or fails when
    /// nothing did. With an <see cref="ICraftingInventory"/>, ingredients are taken at the start, given back on cancel,
    /// and results handed out on completion. A recipe's requirements are checked before it starts (see
    /// <see cref="ICraftingRequirementChecker"/>).
    /// </summary>
    public class CraftingManager : ICraftingManager
    {
        private readonly IEntityManager _entityManager;
        private readonly ICraftingRecipeRegistry _recipeRegistry;
        private readonly ICraftingInventory _inventory;
        private readonly ICraftingRequirementChecker _requirements;
        private readonly IEventPublisher _events;
        private readonly Random _random;
        private readonly List<int> _crafters = new List<int>();
        private readonly List<int> _finished = new List<int>();
        private readonly List<ICraftingResult> _made = new List<ICraftingResult>();
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
        /// <param name="inventory">Where crafters keep their items; null leaves items to the game.</param>
        /// <param name="random">Rolls success chances; null picks a new seed.</param>
        /// <param name="requirements">Judges skill, quest and tool requirements; without it such recipes are refused.</param>
        /// <param name="events">Where the crafting events also go, besides the C# events (the game's bus).</param>
        public CraftingManager(IEntityManager entityManager, ICraftingRecipeRegistry recipeRegistry,
            ICraftingInventory inventory = null, Random random = null, ICraftingRequirementChecker requirements = null,
            IEventPublisher events = null)
        {
            _events = events;
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _recipeRegistry = recipeRegistry ?? throw new ArgumentNullException(nameof(recipeRegistry));
            _inventory = inventory;
            _requirements = requirements;
            _random = random ?? new Random();
        }

        /// <summary>
        /// Whether one requirement of a recipe is met for this crafter at this station. Stations are checked here
        /// (the parameter is a station id or a station type, the value a minimum station level); tools through the
        /// inventory when there is one; everything else through the game's checker. Nothing is taken as met unchecked.
        /// </summary>
        public bool IsRequirementMet(int entityId, ICraftingRequirement requirement, ICraftingStation station)
        {
            if (requirement == null)
                return true;

            switch (requirement.Type)
            {
                case RequirementType.StationRequired:
                    if (station == null)
                        return false;
                    bool sameStation = string.Equals(station.StationId, requirement.ParameterId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(station.StationType.ToString(), requirement.ParameterId, StringComparison.OrdinalIgnoreCase);
                    return sameStation && station.Level >= requirement.Value;

                case RequirementType.ToolRequired when _inventory != null:
                    return _inventory.Has(entityId, requirement.ParameterId, Math.Max(1, requirement.Value));

                default:
                    return _requirements != null && _requirements.IsSatisfied(entityId, requirement);
            }
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

            foreach (var requirement in recipe.Requirements)
            {
                if (!IsRequirementMet(entityId, requirement, station))
                    return -1;
            }

            if (!station.IsAvailable())
                return -1;

            if (_inventory != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!_inventory.Has(entityId, ingredient.ItemId, ingredient.Quantity))
                        return -1;
                }
                foreach (var ingredient in recipe.Ingredients)
                    _inventory.Take(entityId, ingredient.ItemId, ingredient.Quantity);
            }

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

            var craftingStartedEvent = new CraftingStartedEvent(entityId, recipeId, station, recipe.CraftingTimeSeconds);
            CraftingStarted?.Invoke(this, craftingStartedEvent);
            _events?.Publish(craftingStartedEvent);

            return operationId;
        }

        /// <inheritdoc/>
        public void UpdateCrafting(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds < 0)
                throw new ArgumentException("Delta time cannot be negative", nameof(deltaTimeSeconds));

            _entityManager.GetEntitiesWith<ActiveCraftingData>(_crafters);
            foreach (int entityId in _crafters)
            {
                var activeCrafts = _entityManager.GetComponent<ActiveCraftingData>(entityId);
                if (activeCrafts.ActiveCrafts == null || activeCrafts.ActiveCrafts.Count == 0)
                    continue;

                _finished.Clear();
                foreach (var operationId in activeCrafts.ActiveCrafts.Keys.ToList())
                {
                    var operation = activeCrafts.ActiveCrafts[operationId];
                    if (operation.State != CraftingState.InProgress)
                        continue;
                    operation.ElapsedTimeSeconds = Math.Min(operation.RequiredTimeSeconds,
                        operation.ElapsedTimeSeconds + deltaTimeSeconds);
                    activeCrafts.ActiveCrafts[operationId] = operation;
                    if (operation.ElapsedTimeSeconds >= operation.RequiredTimeSeconds)
                        _finished.Add(operationId);
                }

                // Oldest first, so crafts that end in the same update finish in the order they started
                _finished.Sort();
                foreach (int operationId in _finished)
                    Finish(entityId, operationId, activeCrafts.ActiveCrafts[operationId].RecipeId);
            }
        }

        private void Finish(int entityId, int operationId, string recipeId)
        {
            var recipe = _recipeRegistry.GetRecipe(recipeId);
            if (recipe == null)
            {
                FailCrafting(entityId, operationId, $"The recipe '{recipeId}' no longer exists");
                return;
            }

            _made.Clear();
            foreach (var result in recipe.Results)
            {
                if (result.SuccessChance >= 100f || _random.NextDouble() * 100.0 < result.SuccessChance)
                    _made.Add(result);
            }
            if (recipe.Results.Count > 0 && _made.Count == 0)
            {
                FailCrafting(entityId, operationId, "Nothing came out of it");
                return;
            }

            var made = _made.ToList();
            if (_inventory != null)
            {
                foreach (var result in made)
                    _inventory.Give(entityId, result.ItemId, result.Quantity);
            }
            CompleteCrafting(entityId, operationId, made);
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

            // Nothing was made: the ingredients go back
            var recipe = _recipeRegistry.GetRecipe(recipeId);
            if (_inventory != null && recipe != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                    _inventory.Give(entityId, ingredient.ItemId, ingredient.Quantity);
            }

            var craftingCancelledEvent = new CraftingCancelledEvent(entityId, recipeId, timeSpent);
            CraftingCancelled?.Invoke(this, craftingCancelledEvent);
            _events?.Publish(craftingCancelledEvent);

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

            var craftingCompletedEvent = new CraftingCompletedEvent(
                entityId,
                operation.RecipeId,
                results,
                operation.ElapsedTimeSeconds);
            CraftingCompleted?.Invoke(this, craftingCompletedEvent);
            _events?.Publish(craftingCompletedEvent);
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

            var craftingFailedEvent = new CraftingFailedEvent(
                entityId,
                operation.RecipeId,
                failureReason,
                operation.ElapsedTimeSeconds);
            CraftingFailed?.Invoke(this, craftingFailedEvent);
            _events?.Publish(craftingFailedEvent);
        }
    }
}
