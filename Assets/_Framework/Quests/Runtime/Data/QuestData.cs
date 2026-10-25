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
    }
}
