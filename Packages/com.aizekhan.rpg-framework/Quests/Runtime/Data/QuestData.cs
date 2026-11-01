using System;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Serializable quest definition data.
    /// </summary>
    [Serializable]
    public struct QuestData
    {
        public string Id;
        public string Title;
        public string Description;
        public QuestObjectiveData[] Objectives;
        public QuestRewardData[] Rewards;
        public string[] PrerequisiteQuestIds;
        public int MinimumLevel;
        public bool IsRepeatable;

        /// <summary>The game's own kind of quest ("main", "side", "faction"...); free text, optional.</summary>
        public string Type;

        /// <summary>Stages with links between them; empty = the quest completes when its objectives are done.</summary>
        public QuestStageData[] Stages;

        /// <summary>The first stage; empty = the first of <see cref="Stages"/>.</summary>
        public string StartStageId;

        /// <summary>Checked by the game's <see cref="IQuestRequirements"/> before the quest can be taken.</summary>
        public QuestRequirementData[] Requirements;

        /// <summary>Seconds before a repeatable quest can be taken again (86400 for a daily); 0 = at once.</summary>
        public double RepeatCooldownSeconds;
    }
}
