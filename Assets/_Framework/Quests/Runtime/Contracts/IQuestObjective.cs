namespace RPGFramework.Quests
{
    /// <summary>
    /// Represents a single objective within a quest.
    /// </summary>
    public interface IQuestObjective
    {
        /// <summary>
        /// Unique identifier for this objective.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Type of objective (Kill, Collect, Interact, etc.).
        /// </summary>
        ObjectiveType Type { get; }

        /// <summary>
        /// Target identifier (enemy ID, item ID, NPC ID, location ID).
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Required count to complete this objective.
        /// </summary>
        int RequiredCount { get; }

        /// <summary>
        /// Human-readable description of the objective.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Whether this objective is optional for quest completion.
        /// </summary>
        bool IsOptional { get; }
    }
}
