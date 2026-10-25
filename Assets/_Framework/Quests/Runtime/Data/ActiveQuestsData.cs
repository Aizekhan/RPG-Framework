using System;
using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Quests
{
    /// <summary>
    /// ECS component holding all quest progress for an entity.
    /// </summary>
    [Serializable]
    public struct ActiveQuestsData : IComponent
    {
        public Dictionary<string, QuestProgressData> ActiveQuests;
        public List<string> CompletedQuestIds;
        public int TotalQuestsCompleted;
    }
}
