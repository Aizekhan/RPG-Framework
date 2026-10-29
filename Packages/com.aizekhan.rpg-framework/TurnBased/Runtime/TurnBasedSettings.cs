using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Items;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Everything a turn-based run starts from: the party, the enemies, the encounters fought one after another,
    /// what the party carries at the start and the rules that are numbers. Built by a Unity setup asset, or in code by
    /// tests.
    /// </summary>
    public sealed class TurnBasedSettings
    {
        public const int MaxPartySize = 4;

        /// <summary>
        /// The heroes, in party order (1 to <see cref="MaxPartySize"/>).
        /// </summary>
        public List<CombatantData> Party { get; } = new List<CombatantData>();

        public List<CombatantData> Enemies { get; } = new List<CombatantData>();

        /// <summary>
        /// The run: fight 1, fight 2 ... fight N, in this order.
        /// </summary>
        public List<EncounterData> Encounters { get; } = new List<EncounterData>();

        /// <summary>
        /// Item ids in the party inventory at the start, one entry per item.
        /// </summary>
        public List<string> StartingItems { get; } = new List<string>();

        public int StartingGold { get; set; }

        /// <summary>
        /// Quests the party has from the start (and again after <see cref="TurnBasedWorld.Restart"/> when not done yet).
        /// </summary>
        public List<string> StartingQuests { get; } = new List<string>();

        /// <summary>
        /// A quest whose objectives are all done completes (and pays its rewards) by itself; false: the game calls
        /// <see cref="TurnBasedWorld.CompleteQuest"/> (a quest giver to go back to).
        /// </summary>
        public bool CompleteQuestsAutomatically { get; set; } = true;

        /// <summary>
        /// Skill points every party member has at level 1 (<see cref="TurnBasedWorld.Skills"/>).
        /// </summary>
        public int StartingSkillPoints { get; set; }

        /// <summary>
        /// Skill points every level above 1 gives a member.
        /// </summary>
        public int SkillPointsPerLevel { get; set; } = 1;

        public int InventoryCapacity { get; set; } = 60;

        /// <summary>
        /// Chance (0..1) a flee attempt works.
        /// </summary>
        public float FleeChance { get; set; } = 0.5f;

        /// <summary>
        /// Share (0..1) of maximum health and mana every party member gets back after a won fight; fallen members
        /// get up with that share. 0 = no rest: health, mana and the fallen carry over as they are.
        /// </summary>
        public float RestAfterVictory { get; set; }

        /// <summary>
        /// Fixed random seed (hits, misses, enemy choices, flee, loot); 0 picks a new one every run.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Damage and healing maths; null uses <see cref="DefaultBattleFormula"/>.
        /// </summary>
        public IBattleFormula Formula { get; set; }

        /// <summary>
        /// How enemies choose; null uses <see cref="SimpleBattleAi"/>.
        /// </summary>
        public IBattleAi EnemyAi { get; set; }

        /// <summary>
        /// Experience per level; null uses the Progression module's default table.
        /// </summary>
        public ILevelTable LevelTable { get; set; }

        /// <summary>
        /// Enemies act on their own as soon as their turn comes (the default). Off, the game calls
        /// <see cref="Battle.PlayAiTurn"/> for each enemy turn (to pace animations).
        /// </summary>
        public bool EnemiesActAutomatically { get; set; } = true;

        public IEnumerable<CombatantData> Characters => Party.Concat(Enemies).Where(c => c != null);

        public CombatantData FindEnemy(string id) => Enemies.FirstOrDefault(e => e != null && e.Id == id);

        /// <summary>
        /// Mistakes that would break or silently empty the game, in words a designer understands.
        /// </summary>
        public List<string> Validate(TurnBasedContent content)
        {
            content = content ?? new TurnBasedContent();
            var problems = new List<string>();

            int members = Party.Count(p => p != null);
            if (members == 0)
                problems.Add("The party is empty.");
            if (members > MaxPartySize)
                problems.Add($"The party has {members} members; at most {MaxPartySize} fit.");
            if (Encounters.Count(e => e != null) == 0)
                problems.Add("There are no encounters to fight.");
            if (FleeChance < 0f || FleeChance > 1f)
                problems.Add("Flee Chance must be between 0 and 1.");
            if (RestAfterVictory < 0f || RestAfterVictory > 1f)
                problems.Add("Rest After Victory must be between 0 and 1.");

            var characters = Characters.ToList();
            foreach (var blank in characters.Where(c => string.IsNullOrWhiteSpace(c.Id)))
                problems.Add($"Character '{blank.DisplayName}' has no id.");
            foreach (var duplicate in characters.GroupBy(c => c.Id).Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key)))
                problems.Add($"Several characters use the id '{duplicate.Key}'.");
            foreach (var character in characters.Where(c => c.MaxHealth <= 0f))
                problems.Add($"{character.DisplayName}: Max Health must be above 0.");

            var items = new HashSet<string>(content.Items.Where(i => i != null).Select(i => i.ItemId));
            var tables = new HashSet<string>(content.LootTables.Where(t => t != null).Select(t => t.TableId));
            var abilities = content.Abilities.Where(a => a != null && !string.IsNullOrEmpty(a.AbilityId))
                .GroupBy(a => a.AbilityId).ToDictionary(g => g.Key, g => g.First());
            var effects = new HashSet<string>(content.StatusEffects.Where(s => s != null).Select(s => s.Id));

            foreach (var character in characters)
            {
                if (!string.IsNullOrEmpty(character.LootTableId) && !tables.Contains(character.LootTableId))
                    problems.Add($"{character.DisplayName}: loot table '{character.LootTableId}' is not in the setup.");
                foreach (var abilityId in (character.AbilityIds ?? new List<string>()).Where(id => !abilities.ContainsKey(id)))
                    problems.Add($"{character.DisplayName} knows '{abilityId}', which is not an ability in the setup.");
            }

            foreach (var ability in abilities.Values)
            {
                if (ability.CastInfo.CastTime > 0f || ability.CastInfo.ChannelDuration > 0f)
                    problems.Add($"Ability '{ability.Name}' has a cast or channel time; turn-based battles use abilities at once, set them to 0.");
                if (ability.Type != AbilityType.Active && ability.Type != AbilityType.Triggered)
                    problems.Add($"Ability '{ability.Name}' is {ability.Type}; only Active (and Triggered) abilities can be used in battle.");
            }

            foreach (var effect in content.AbilityEffects.Where(e => e != null))
            {
                if (!abilities.ContainsKey(effect.AbilityId))
                    problems.Add($"Ability effect '{effect.AbilityId}' is for an ability that is not in the setup.");
                foreach (var missing in effect.StatusEffectIds.Concat(effect.RemovesEffectIds ?? new List<string>()).Where(id => !effects.Contains(id)))
                    problems.Add($"Ability '{effect.AbilityId}' uses status effect '{missing}', which is not in the setup.");
                if (effect.Revives && effect.Scope != TargetScope.SingleAlly)
                    problems.Add($"Ability '{effect.AbilityId}' revives, so it must target a single ally.");
            }

            foreach (var table in content.LootTables.Where(t => t != null))
            {
                var dropped = table.Entries.Select(e => e.ItemId).Concat(table.GuaranteedDrops ?? Enumerable.Empty<string>());
                foreach (var missing in dropped.Where(id => !items.Contains(id)).Distinct())
                    problems.Add($"Loot table '{table.TableId}' drops '{missing}', which is not an item in the setup.");
            }

            var enemyIds = new HashSet<string>(Enemies.Where(e => e != null).Select(e => e.Id));
            foreach (var encounter in Encounters.Where(e => e != null))
            {
                if (encounter.EnemyIds == null || encounter.EnemyIds.Count == 0)
                    problems.Add($"Encounter '{encounter.DisplayName}' has no enemies.");
                foreach (var missing in (encounter.EnemyIds ?? new List<string>()).Where(id => !enemyIds.Contains(id)).Distinct())
                    problems.Add($"Encounter '{encounter.DisplayName}' lists '{missing}', which is not an enemy in the setup.");
            }

            foreach (var missing in StartingItems.Where(id => !items.Contains(id)).Distinct())
                problems.Add($"Starting item '{missing}' is not an item in the setup.");

            var questIds = new HashSet<string>(content.Quests.Where(q => q != null).Select(q => q.Id));
            foreach (var missing in StartingQuests.Where(id => !questIds.Contains(id)).Distinct())
                problems.Add($"Starting quest '{missing}' is not a quest in the setup.");

            return problems;
        }
    }

    /// <summary>
    /// The game's content in runtime form: registered into the framework registries when the game starts. Abilities,
    /// status effects, items and loot tables are what the designer assets produce (AbilityAsset.ToAbility,
    /// StatusEffectAsset.ToData ...).
    /// </summary>
    public sealed class TurnBasedContent
    {
        public List<IItem> Items { get; } = new List<IItem>();
        public List<ILootTable> LootTables { get; } = new List<ILootTable>();
        public List<IAbility> Abilities { get; } = new List<IAbility>();
        public List<StatusEffectData> StatusEffects { get; } = new List<StatusEffectData>();

        /// <summary>
        /// What each ability does when it lands, by ability id.
        /// </summary>
        public List<AbilityEffectData> AbilityEffects { get; } = new List<AbilityEffectData>();

        /// <summary>
        /// The game's quests: beaten enemies advance Kill objectives (by character id), items the party gets advance
        /// Collect ones (by item id) and won encounters advance Explore ones (by encounter id).
        /// </summary>
        public List<RPGFramework.Quests.IQuest> Quests { get; } = new List<RPGFramework.Quests.IQuest>();

        /// <summary>
        /// The game's achievements, for the whole party (<see cref="TurnBasedWorld.Achievements"/>): beaten enemies by
        /// character id (KillCount), items got (ItemCollected), completed quests (QuestCompleted), the highest level a
        /// member reached (LevelReached) and the damage the party deals (DamageDealt).
        /// </summary>
        public List<RPGFramework.Achievements.Runtime.Contracts.IAchievement> Achievements { get; } =
            new List<RPGFramework.Achievements.Runtime.Contracts.IAchievement>();

        /// <summary>
        /// The skills party members learn with the points their levels give (<see cref="TurnBasedWorld.Skills"/>);
        /// bonuses are <see cref="CombatStat"/> names (or aliases) or "resist:fire". Null: no skill tree.
        /// </summary>
        public RPGFramework.Skills.ISkillTree SkillTree { get; set; }
    }

    /// <summary>
    /// What a consumable does in battle, read from its <see cref="IConsumable.EffectId"/> and
    /// <see cref="IConsumable.EffectMagnitude"/>. Any other effect id is a status effect id: the item puts that
    /// status effect on the target (an ally or an enemy).
    /// </summary>
    public static class ItemEffects
    {
        /// <summary>
        /// Heals a living ally by the magnitude.
        /// </summary>
        public const string RestoreHealth = "RestoreHealth";

        /// <summary>
        /// Gives a living ally the magnitude in mana.
        /// </summary>
        public const string RestoreMana = "RestoreMana";

        /// <summary>
        /// Brings a fallen ally back: magnitude up to 1 is a share of maximum health, above 1 a flat amount.
        /// </summary>
        public const string Revive = "Revive";

        /// <summary>
        /// Takes every harmful status effect (debuffs, damage over time, conditions) off a living ally.
        /// </summary>
        public const string Cure = "Cure";

        /// <summary>
        /// Deals the magnitude as damage to a living enemy (a bomb); it never misses.
        /// </summary>
        public const string Damage = "Damage";

        public static string Normalize(string effectId)
        {
            switch ((effectId ?? "").Trim().ToLowerInvariant())
            {
                case "restorehealth":
                case "restorehp":
                case "heal": return RestoreHealth;
                case "restoremana":
                case "restoremp": return RestoreMana;
                case "revive": return Revive;
                case "cure": return Cure;
                case "damage": return Damage;
                default: return effectId;
            }
        }
    }
}
