using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuest.
    /// Represents a quest definition with objectives and rewards.
    /// </summary>
    public class Quest : IStagedQuest
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<IQuestObjective> Objectives { get; }
        public IReadOnlyList<IQuestReward> Rewards { get; }
        public IReadOnlyList<string> PrerequisiteQuestIds { get; }
        public int MinimumLevel { get; }
        public bool IsRepeatable { get; }
        public string Type { get; }
        public IReadOnlyList<QuestStageData> Stages { get; } = new QuestStageData[0];
        public string StartStageId { get; }
        public IReadOnlyList<QuestRequirementData> Requirements { get; } = new QuestRequirementData[0];
        public double RepeatCooldownSeconds { get; }

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
            Type = data.Type ?? "";
            Stages = data.Stages ?? new QuestStageData[0];
            StartStageId = !string.IsNullOrEmpty(data.StartStageId) ? data.StartStageId
                : Stages.Count > 0 ? Stages[0].Id : null;
            Requirements = data.Requirements ?? new QuestRequirementData[0];
            RepeatCooldownSeconds = Math.Max(0d, data.RepeatCooldownSeconds);
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
