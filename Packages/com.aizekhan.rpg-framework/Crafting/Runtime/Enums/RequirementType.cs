namespace RPGFramework.Crafting
{
    /// <summary>
    /// Types of requirements that can be enforced for crafting recipes.
    /// </summary>
    public enum RequirementType
    {
        /// <summary>
        /// Requires a specific skill level to craft.
        /// </summary>
        SkillLevel = 0,

        /// <summary>
        /// Requires a specific tool item to be in inventory.
        /// </summary>
        ToolRequired = 1,

        /// <summary>
        /// Requires the recipe to be at a specific station type.
        /// </summary>
        StationRequired = 2,

        /// <summary>
        /// Requires a quest to be completed before crafting.
        /// </summary>
        QuestComplete = 3
    }
}
