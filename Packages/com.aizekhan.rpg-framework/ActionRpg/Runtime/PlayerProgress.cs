using System.Collections.Generic;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What carries over between play sessions: the player's level and experience, the backpack, the money and the quests.
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

        /// <summary>
        /// Balances in the wallet (gold and any other currency of <see cref="ActionRpgSettings.Currencies"/>).
        /// </summary>
        public List<CurrencyRecord> Currencies { get; set; } = new List<CurrencyRecord>();

        /// <summary>
        /// What the player wears, in every set of theirs (<see cref="ActorGear"/>); not in <see cref="Items"/>.
        /// </summary>
        public List<GearRecord> Gear { get; set; } = new List<GearRecord>();

        /// <summary>
        /// The set the player wears (<see cref="ActorGear.UseLoadout"/>).
        /// </summary>
        public string Loadout { get; set; } = "";

        public List<QuestRecord> ActiveQuests { get; set; } = new List<QuestRecord>();
        public List<string> CompletedQuests { get; set; } = new List<string>();

        /// <summary>
        /// What the player learned in the skill tree (<see cref="ActorSkills"/>).
        /// </summary>
        public List<SkillRecord> Skills { get; set; } = new List<SkillRecord>();

        /// <summary>
        /// Skill points granted beyond the levels (<see cref="ActorSkills.GrantPoints"/>).
        /// </summary>
        public int BonusSkillPoints { get; set; }
    }

    public sealed class CurrencyRecord
    {
        public string Currency { get; set; }
        public long Amount { get; set; }
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
