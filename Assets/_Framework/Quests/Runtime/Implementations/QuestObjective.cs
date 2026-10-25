namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestObjective.
    /// Represents a single objective within a quest.
    /// </summary>
    public class QuestObjective : IQuestObjective
    {
        public string Id { get; }
        public ObjectiveType Type { get; }
        public string TargetId { get; }
        public int RequiredCount { get; }
        public string Description { get; }
        public bool IsOptional { get; }

        /// <summary>
        /// Create a QuestObjective from QuestObjectiveData.
        /// </summary>
        public QuestObjective(QuestObjectiveData data)
        {
            Id = data.Id;
            Type = data.Type;
            TargetId = data.TargetId;
            RequiredCount = data.RequiredCount;
            Description = data.Description;
            IsOptional = data.IsOptional;
        }

        /// <summary>
        /// Create a QuestObjective directly with parameters.
        /// </summary>
        public QuestObjective(
            string id,
            ObjectiveType type,
            string targetId,
            int requiredCount,
            string description,
            bool isOptional = false)
        {
            Id = id;
            Type = type;
            TargetId = targetId;
            RequiredCount = requiredCount;
            Description = description;
            IsOptional = isOptional;
        }
    }
}
