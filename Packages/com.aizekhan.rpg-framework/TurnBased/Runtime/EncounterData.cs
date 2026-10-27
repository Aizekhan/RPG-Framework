using System;
using System.Collections.Generic;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// One fight of the run: which enemies come (by <see cref="CombatantData"/> id, the same id may repeat:
    /// "goblin, goblin, shaman") and whether the party may run from it.
    /// </summary>
    [Serializable]
    public sealed class EncounterData
    {
        public string Id = "encounter";

        public string DisplayName = "Encounter";

        /// <summary>
        /// Enemy ids in battle order; duplicates make several of the same enemy ("Goblin A", "Goblin B").
        /// </summary>
        public List<string> EnemyIds = new List<string>();

        /// <summary>
        /// Level of the enemies (their per-level growth applies above 1).
        /// </summary>
        public int EnemyLevel = 1;

        /// <summary>
        /// The party may try to flee; false for boss fights.
        /// </summary>
        public bool CanFlee = true;
    }
}
