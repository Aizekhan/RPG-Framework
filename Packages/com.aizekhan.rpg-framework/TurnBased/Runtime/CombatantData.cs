using System;
using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Progression;

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

        /// <summary>
        /// Elements it resists (0.5: half damage), is weak to (-0.5) or is immune to, against abilities with an
        /// <see cref="AbilityEffectData.Element"/>.
        /// </summary>
        public List<ElementResistance> Resistances = new List<ElementResistance>();

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
        /// Its own chance (0..1) to land a critical hit, added to the formula's
        /// (<see cref="DefaultBattleFormula.CriticalChance"/>): a rogue 0.2, a boss 0.
        /// </summary>
        public float CriticalChance;

        /// <summary>
        /// The stat at a level, before status effects.
        /// </summary>
        public float StatAt(CombatStat stat, int level) => Growth(stat).CalculateAtLevel(level);

        /// <summary>
        /// How the stat grows, as Progression growth (linear: the base value plus the per-level gain above level 1).
        /// The world registers it as the hero's growth profile, so level-ups publish <see cref="StatIncreasedEvent"/>.
        /// </summary>
        public StatGrowthData Growth(CombatStat stat)
        {
            string id = stat.ToString();
            switch (stat)
            {
                case CombatStat.MaxHealth: return new StatGrowthData(id, MaxHealth, HealthPerLevel);
                case CombatStat.MaxMana: return new StatGrowthData(id, MaxMana, ManaPerLevel);
                case CombatStat.Attack: return new StatGrowthData(id, Attack, AttackPerLevel);
                case CombatStat.Defense: return new StatGrowthData(id, Defense, DefensePerLevel);
                case CombatStat.Magic: return new StatGrowthData(id, Magic, MagicPerLevel);
                case CombatStat.Speed: return new StatGrowthData(id, Speed, SpeedPerLevel);
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }

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
