using System;
using System.Collections.Generic;
using RPGFramework.Combat;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// What a kind of character is: its stats, how it grows and what killing it gives. Engine-free; the Unity
    /// <c>CharacterDefinition</c> asset produces it.
    /// </summary>
    [Serializable]
    public sealed class CharacterData
    {
        /// <summary>
        /// Unique id; also the prefab key and the target id of Kill quest objectives ("goblin").
        /// </summary>
        public string Id = "character";

        public string DisplayName = "Character";

        public float MaxHealth = 100f;
        public float Attack = 10f;
        public float Defense = 2f;

        /// <summary>
        /// Stat gained per level above 1 (the player's levels; an enemy's when <see cref="ActionRpgSettings.EnemyLevels"/> raises them).
        /// </summary>
        public float HealthPerLevel;

        public float AttackPerLevel;
        public float DefensePerLevel;

        /// <summary>
        /// Experience the killer gets.
        /// </summary>
        public long ExperienceReward;

        /// <summary>
        /// Experience the killer gets more for each level of this character above 1 (enemies that grow with the player).
        /// </summary>
        public long ExperiencePerLevel;

        /// <summary>
        /// Loot table rolled for the killer; empty for none.
        /// </summary>
        public string LootTableId = "";

        /// <summary>
        /// The faction it belongs to (<see cref="ActionRpgContent.Factions"/>); empty for none.
        /// </summary>
        public string Faction = "";

        /// <summary>
        /// How the killer's reputation with its <see cref="Faction"/> changes when it is killed (-10: they hate it).
        /// </summary>
        public int ReputationOnKill;

        /// <summary>
        /// Units per second it moves; a co-op host corrects joined players who move faster. 0 = not checked.
        /// </summary>
        public float MoveSpeed;

        /// <summary>
        /// Chance (0..1) its strikes are critical (<see cref="ActorStat.CriticalChance"/>: gear, affixes and effects add
        /// to it). 0: it never crits and no roll is made.
        /// </summary>
        public float CriticalChance;

        /// <summary>
        /// What a critical strike's damage is multiplied by (<see cref="ActorStat.CriticalDamage"/>).
        /// </summary>
        public float CriticalDamage = 1.5f;

        /// <summary>
        /// Chance (0..1) a strike on it misses (<see cref="ActorStat.Evasion"/>); effects and other damage still land.
        /// </summary>
        public float Evasion;

        /// <summary>
        /// Share of its strikes' damage it heals back (<see cref="ActorStat.LifeSteal"/>: 0.1 = a tenth).
        /// </summary>
        public float LifeSteal;

        /// <summary>
        /// What it resists, is weak to or is immune to ("fire" 0.5, "ice" -0.5); which hits carry an element is
        /// <see cref="ActionRpgSettings.ElementTags"/>.
        /// </summary>
        public List<ElementResistance> Resistances = new List<ElementResistance>();

        public float StatAt(ActorStat stat, int level)
        {
            int above = Math.Max(0, level - 1);
            switch (stat)
            {
                case ActorStat.MaxHealth: return MaxHealth + HealthPerLevel * above;
                case ActorStat.Attack: return Attack + AttackPerLevel * above;
                case ActorStat.Defense: return Defense + DefensePerLevel * above;
                case ActorStat.CriticalChance: return CriticalChance;
                case ActorStat.CriticalDamage: return CriticalDamage;
                case ActorStat.Evasion: return Evasion;
                case ActorStat.LifeSteal: return LifeSteal;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }

    public enum ActorStat
    {
        MaxHealth,
        Attack,
        Defense,

        /// <summary>
        /// Chance (0..1) a strike is critical; "CriticalChance" in gear bonuses, affixes and status effects.
        /// </summary>
        CriticalChance,

        /// <summary>
        /// A critical strike's damage multiplier (1.5 = +50%).
        /// </summary>
        CriticalDamage,

        /// <summary>
        /// Chance (0..1) a strike on the character misses.
        /// </summary>
        Evasion,

        /// <summary>
        /// Share of the character's strike damage healed back.
        /// </summary>
        LifeSteal
    }
}
