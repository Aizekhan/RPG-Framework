using System.Collections.Generic;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What carries over between play sessions: the player's level and experience, the backpack and the quests.
    /// Enemies and waves start fresh every session.
    /// </summary>
    public sealed class PlayerProgress
    {
        public int Level { get; set; } = 1;
        public long TotalExperience { get; set; }
        public int Kills { get; set; }

        /// <summary>
        /// Item ids in backpack order, one entry per item.
        /// </summary>
        public List<string> Items { get; set; } = new List<string>();

        public List<QuestRecord> ActiveQuests { get; set; } = new List<QuestRecord>();
        public List<string> CompletedQuests { get; set; } = new List<string>();
    }

    public sealed class QuestRecord
    {
        public string QuestId { get; set; }
        public List<ObjectiveRecord> Objectives { get; set; } = new List<ObjectiveRecord>();
    }

    public sealed class ObjectiveRecord
    {
        public string ObjectiveId { get; set; }
        public int Progress { get; set; }
    }
}
