using System.Collections.Generic;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// What a save keeps of a run: each member's level, experience, health and mana, the inventory, the gold, the
    /// quests and how far the run got. A battle in progress is not kept: loading puts the party before the next encounter.
    /// </summary>
    public sealed class PartyProgress
    {
        public int EncounterIndex { get; set; }
        public int Gold { get; set; }
        public bool RunOver { get; set; }

        /// <summary>
        /// Item ids in inventory order, one entry per item.
        /// </summary>
        public List<string> Items { get; set; } = new List<string>();

        public List<MemberRecord> Members { get; set; } = new List<MemberRecord>();

        public List<PartyQuestRecord> ActiveQuests { get; set; } = new List<PartyQuestRecord>();
        public List<string> CompletedQuests { get; set; } = new List<string>();
    }

    /// <summary>
    /// A quest the party is on, with how far each objective got.
    /// </summary>
    public sealed class PartyQuestRecord
    {
        public string QuestId { get; set; }
        public List<PartyObjectiveRecord> Objectives { get; set; } = new List<PartyObjectiveRecord>();
    }

    public sealed class PartyObjectiveRecord
    {
        public string ObjectiveId { get; set; }
        public int Progress { get; set; }
    }

    public sealed class MemberRecord
    {
        /// <summary>
        /// The member's <see cref="CombatantData.Id"/> (members are matched by it, so reordering the party keeps saves).
        /// </summary>
        public string CharacterId { get; set; }

        public int Level { get; set; } = 1;
        public long TotalExperience { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }

        /// <summary>
        /// What the member learned in the skill tree (<see cref="PartySkills"/>).
        /// </summary>
        public List<MemberSkillRecord> Skills { get; set; } = new List<MemberSkillRecord>();

        public int BonusSkillPoints { get; set; }
    }

    public sealed class MemberSkillRecord
    {
        public string SkillId { get; set; }
        public int Level { get; set; }
    }
}
