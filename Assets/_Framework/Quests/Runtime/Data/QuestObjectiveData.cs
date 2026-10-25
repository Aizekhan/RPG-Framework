using System;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Serializable quest objective data.
    /// </summary>
    [Serializable]
    public struct QuestObjectiveData
    {
        public string Id;
        public ObjectiveType Type;
        public string TargetId;
        public int RequiredCount;
        public string Description;
        public bool IsOptional;
    }
}
