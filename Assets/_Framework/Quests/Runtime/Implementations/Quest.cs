using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuest.
    /// Represents a quest definition with objectives and rewards.
    /// </summary>
    public class Quest : IQuest
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<IQuestObjective> Objectives { get; }
        public IReadOnlyList<IQuestReward> Rewards { get; }
        public IReadOnlyList<string> PrerequisiteQuestIds { get; }
        public int MinimumLevel { get; }
        public bool IsRepeatable { get; }

        /// <summary>
        /// Create a Quest from QuestData.
        /// </summary>
        public Quest(QuestData data)
        {
            Id = data.Id;
            Title = data.Title;
            Description = data.Description;
            MinimumLevel = data.MinimumLevel;
            IsRepeatable = data.IsRepeatable;

            Objectives = data.Objectives?
                .Select(o => new QuestObjective(o))
                .ToList()
                ?? new List<QuestObjective>();

            Rewards = data.Rewards?
                .Select(r => new QuestReward(r))
                .ToList()
                ?? new List<QuestReward>();

            PrerequisiteQuestIds = data.PrerequisiteQuestIds ?? new string[0];
        }

        /// <summary>
        /// Create a Quest directly with parameters.
        /// </summary>
        public Quest(
            string id,
            string title,
            string description,
            IReadOnlyList<IQuestObjective> objectives,
            IReadOnlyList<IQuestReward> rewards,
            IReadOnlyList<string> prerequisiteQuestIds,
            int minimumLevel,
            bool isRepeatable)
        {
            Id = id;
            Title = title;
            Description = description;
            Objectives = objectives ?? new List<IQuestObjective>();
            Rewards = rewards ?? new List<IQuestReward>();
            PrerequisiteQuestIds = prerequisiteQuestIds ?? new List<string>();
            MinimumLevel = minimumLevel;
            IsRepeatable = isRepeatable;
        }
    }
}
