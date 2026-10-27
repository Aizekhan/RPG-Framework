namespace RPGFramework.Crafting
{
    /// <summary>
    /// Answers the recipe requirements only the game can judge: skill levels, quests, and tools when the manager has
    /// no <see cref="ICraftingInventory"/>. The crafting manager checks station requirements itself. Without a
    /// checker, a recipe with such a requirement cannot be started: an unchecked requirement is never taken as met.
    /// </summary>
    public interface ICraftingRequirementChecker
    {
        bool IsSatisfied(int entityId, ICraftingRequirement requirement);
    }
}
