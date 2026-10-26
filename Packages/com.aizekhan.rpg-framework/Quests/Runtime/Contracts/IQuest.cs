using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Represents a quest definition with objectives and rewards.
    /// </summary>
    public interface IQuest
    {
        /// <summary>
        /// Unique identifier for this quest.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display title of the quest.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Full description of the quest.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// List of objectives that must be completed.
        /// </summary>
        IReadOnlyList<IQuestObjective> Objectives { get; }

        /// <summary>
        /// Rewards given upon quest completion.
        /// </summary>
        IReadOnlyList<IQuestReward> Rewards { get; }

        /// <summary>
        /// IDs of quests that must be completed before this quest can be accepted.
        /// </summary>
        IReadOnlyList<string> PrerequisiteQuestIds { get; }

        /// <summary>
        /// Minimum character level required to accept this quest.
        /// </summary>
        int MinimumLevel { get; }

        /// <summary>
        /// Whether this quest can be repeated after completion.
        /// </summary>
        bool IsRepeatable { get; }
    }
}
