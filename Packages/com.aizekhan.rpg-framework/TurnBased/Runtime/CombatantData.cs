using System;
using System.Collections.Generic;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// What a kind of fighter is, hero or monster: its stats, how they grow per level, the abilities it knows and
    /// what defeating it gives. Abilities and the loot table are referenced by id, so the designer assets
    /// (AbilityAsset, LootTableAsset) can feed them. Engine-free.
    /// </summary>
    [Serializable]
    public sealed class CombatantData
    {
        /// <summary>
        /// Unique id; encounters list their enemies by it ("goblin").
        /// </summary>
        public string Id = "combatant";

        public string DisplayName = "Combatant";

        public float MaxHealth = 100f;
        public float MaxMana = 20f;
        public float Attack = 10f;
        public float Defense = 5f;
        public float Magic = 5f;

        /// <summary>
        /// Who acts first in a round: higher goes earlier.
        /// </summary>
        public float Speed = 10f;

        /// <summary>
        /// Stat gained per level above 1.
        /// </summary>
        public float HealthPerLevel;

        public float ManaPerLevel;
        public float AttackPerLevel;
        public float DefensePerLevel;
        public float MagicPerLevel;
        public float SpeedPerLevel;

        /// <summary>
        /// Ability ids it may use in battle (registered abilities; what each does is an <see cref="AbilityEffectData"/>).
        /// </summary>
        public List<string> AbilityIds = new List<string>();

        /// <summary>
        /// Experience every living party member gets when this enemy's side is beaten.
        /// </summary>
        public long ExperienceReward;

        /// <summary>
        /// Gold the party gets when this enemy's side is beaten.
        /// </summary>
        public int Gold;

        /// <summary>
        /// Loot table rolled for the party when this enemy's side is beaten; empty for none.
        /// </summary>
        public string LootTableId = "";

        /// <summary>
        /// The stat at a level, before status effects.
        /// </summary>
        public float StatAt(CombatStat stat, int level)
        {
            int above = Math.Max(0, level - 1);
            switch (stat)
            {
                case CombatStat.MaxHealth: return MaxHealth + HealthPerLevel * above;
                case CombatStat.MaxMana: return MaxMana + ManaPerLevel * above;
                case CombatStat.Attack: return Attack + AttackPerLevel * above;
                case CombatStat.Defense: return Defense + DefensePerLevel * above;
                case CombatStat.Magic: return Magic + MagicPerLevel * above;
                case CombatStat.Speed: return Speed + SpeedPerLevel * above;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }

    /// <summary>
    /// The stats of a combatant. Status effects change them by name (a Stat modifier with TargetId "Attack").
    /// </summary>
    public enum CombatStat
    {
        MaxHealth,
        MaxMana,
        Attack,
        Defense,
        Magic,
        Speed
    }

    public static class CombatStats
    {
        /// <summary>
        /// Reads a stat name as status effects and designers write it: the enum name in any case, or the common
        /// aliases "AttackPower", "MagicPower", "Armor", "MaxHP", "MaxMP".
        /// </summary>
        public static bool TryParse(string name, out CombatStat stat)
        {
            stat = default;
            if (string.IsNullOrWhiteSpace(name))
                return false;
            if (Enum.TryParse(name.Trim(), true, out stat) && Enum.IsDefined(typeof(CombatStat), stat))
                return true;
            switch (name.Trim().ToLowerInvariant())
            {
                case "attackpower": stat = CombatStat.Attack; return true;
                case "magicpower": stat = CombatStat.Magic; return true;
                case "armor": stat = CombatStat.Defense; return true;
                case "maxhp": stat = CombatStat.MaxHealth; return true;
                case "maxmp": stat = CombatStat.MaxMana; return true;
                default: return false;
            }
        }
    }
}
