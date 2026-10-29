using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Crafting;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Crafting for the players of an <see cref="ActorWorld"/> (<see cref="ActionRpgContent.Recipes"/>, Crafting
    /// module): a craft takes its ingredients from the player's backpack when it starts, takes the recipe's time
    /// (while the game is not paused) and puts what it made in the same backpack. A recipe with no station level
    /// can be made anywhere; others need a station of their kind and level that lists them (an anvil in the scene). Requirements:
    /// a skill level (<see cref="ActorWorld.Skills"/>), a finished quest, a tool in the backpack. A save made during a
    /// craft keeps its ingredients, not the craft.
    /// </summary>
    public sealed class ActorCrafting : IUpdateSystem, ICraftingInventory, ICraftingRequirementChecker
    {
        private readonly ActorWorld _world;
        private readonly RPGFramework.Events.IEventPublisher _events;
        private readonly CraftingRecipeRegistry _recipes = new CraftingRecipeRegistry();
        private CraftingManager _manager;

        internal ActorCrafting(ActorWorld world, RPGFramework.Events.IEventPublisher events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
        }

        public ICraftingRecipeRegistry Recipes => _recipes;

        private CraftingManager Manager => _manager ??= new CraftingManager(_world.Entities, _recipes, this,
            new Random(_world.Settings.Seed != 0 ? _world.Settings.Seed + 17 : Environment.TickCount), this, _events);

        internal void Register(ICraftingRecipe recipe)
        {
            if (recipe != null)
                _recipes.RegisterRecipe(recipe);
        }

        public ICraftingRecipe Find(string recipeId) => string.IsNullOrEmpty(recipeId) ? null : _recipes.GetRecipe(recipeId);

        /// <summary>
        /// Why the player cannot start this recipe here now, or null when they can.
        /// </summary>
        /// <param name="station">Where they craft; null: by hand (only recipes with no station level).</param>
        public string WhyNot(int playerId, string recipeId, ICraftingStation station = null)
        {
            var recipe = Find(recipeId);
            if (recipe == null)
                return "Unknown recipe";
            if (_world.ProgressOwnerOf(playerId) < 0 || _world.Get(playerId) == null)
                return "Not a player in the party";
            station ??= Hands(recipe);
            if (!station.CanCraft(recipe))
                return "Cannot be made here";
            if (!station.IsAvailable())
                return "The station is busy";
            foreach (var requirement in recipe.Requirements)
            {
                if (!Manager.IsRequirementMet(playerId, requirement, station))
                    return "Its requirements are not met";
            }
            foreach (var ingredient in recipe.Ingredients)
            {
                if (!Has(playerId, ingredient.ItemId, ingredient.Quantity))
                    return "Missing ingredients";
            }
            return null;
        }

        /// <summary>
        /// Starts a craft; its ingredients leave the backpack now.
        /// </summary>
        /// <returns>The craft's id, or -1 when <see cref="WhyNot"/> says why.</returns>
        public int Start(int playerId, string recipeId, ICraftingStation station = null)
        {
            if (WhyNot(playerId, recipeId, station) != null)
                return -1;
            return Manager.StartCrafting(playerId, recipeId, station ?? Hands(Find(recipeId)));
        }

        /// <summary>
        /// Stops a craft; its ingredients come back.
        /// </summary>
        public bool Cancel(int playerId, int craftId) => Manager.CancelCrafting(playerId, craftId);

        public IReadOnlyList<ActiveCraftingOperation> ActiveOf(int playerId) => Manager.GetActiveCrafts(playerId);

        public float ProgressOf(int playerId, int craftId) => Manager.GetCraftingProgress(playerId, craftId);

        public bool IsCrafting(int playerId) => Manager.IsCrafting(playerId);

        /// <summary>
        /// The recipes a player could start by hand now.
        /// </summary>
        public IReadOnlyList<ICraftingRecipe> CraftableBy(int playerId) =>
            _recipes.GetAllRecipes().Where(r => WhyNot(playerId, r.RecipeId) == null).ToList();

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            if (_manager == null || !_world.IsStarted || _world.IsPaused || deltaTime <= 0f)
                return;
            _manager.UpdateCrafting(deltaTime);
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// The ingredients of a player's crafts in progress, as if they were back in the backpack (for a save).
        /// </summary>
        internal IEnumerable<string> IngredientsInProgress(int playerId)
        {
            if (_manager == null)
                yield break;
            foreach (var craft in _manager.GetActiveCrafts(playerId).Where(c => c.State == CraftingState.InProgress))
            {
                var recipe = Find(craft.RecipeId);
                foreach (var ingredient in recipe?.Ingredients ?? Array.Empty<ICraftingIngredient>())
                {
                    for (int i = 0; i < ingredient.Quantity; i++)
                        yield return ingredient.ItemId;
                }
            }
        }

        // Crafts in progress stop (a load, a player leaving); their ingredients go back into the backpack
        internal void CancelAll(int playerId)
        {
            if (_manager == null)
                return;
            foreach (var craft in _manager.GetActiveCrafts(playerId).ToList())
                _manager.CancelCrafting(playerId, craft.OperationId);
        }

        private static ICraftingStation Hands(ICraftingRecipe recipe) =>
            new CraftingStation("hands", "Hands", recipe?.StationType ?? CraftingStationType.Forge, 0,
                recipe != null ? new[] { recipe.RecipeId } : null);

        // ---- The backpack and the requirements, as the crafting manager asks for them ----

        public bool Has(int entityId, string itemId, int quantity) => Count(entityId, itemId) >= quantity;

        public bool Take(int entityId, string itemId, int quantity)
        {
            var inventory = _world.InventoryOf(entityId);
            if (inventory == null || Count(entityId, itemId) < quantity)
                return false;
            foreach (var item in inventory.GetAllItems().Where(i => i != null && i.ItemId == itemId).Take(quantity).ToList())
                inventory.Remove(item);
            return true;
        }

        public void Give(int entityId, string itemId, int quantity)
        {
            if (quantity > 0 && _world.InventoryOf(entityId) != null)
                _world.GiveItem(entityId, itemId, quantity);
        }

        public bool IsSatisfied(int entityId, ICraftingRequirement requirement)
        {
            switch (requirement.Type)
            {
                case RequirementType.SkillLevel:
                    return _world.Skills.LevelOf(entityId, requirement.ParameterId) >= requirement.Value;
                case RequirementType.QuestComplete:
                    return _world.CompletedQuestsOf(entityId).Contains(requirement.ParameterId);
                default:
                    return false;
            }
        }

        private int Count(int entityId, string itemId) =>
            _world.InventoryOf(entityId)?.GetAllItems().Count(i => i != null && i.ItemId == itemId) ?? 0;
    }
}
