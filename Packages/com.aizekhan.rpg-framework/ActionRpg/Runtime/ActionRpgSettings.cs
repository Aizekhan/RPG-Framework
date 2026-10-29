using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Combat;
using RPGFramework.Items;
using RPGFramework.StatusEffects;
using RPGFramework.Loot;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Everything an action RPG starts from: who the player is, which enemies exist, how waves come, which quests
    /// the player starts with. Built by the Unity <c>GameSetup</c> asset, or in code by tests.
    /// </summary>
    public sealed class ActionRpgSettings
    {
        public CharacterData Player { get; set; }
        public List<CharacterData> Enemies { get; } = new List<CharacterData>();
        public WaveSettings Waves { get; set; } = new WaveSettings();

        /// <summary>
        /// Quests the player has from the start.
        /// </summary>
        public List<string> StartingQuests { get; } = new List<string>();

        /// <summary>
        /// Turn a quest in as soon as all its objectives are done (no quest giver needed).
        /// </summary>
        public bool CompleteQuestsAutomatically { get; set; } = true;

        public int InventoryCapacity { get; set; } = 40;

        /// <summary>
        /// In co-op: one progress for the whole party (the first player's), or each player their own.
        /// </summary>
        public ProgressMode ProgressMode { get; set; } = ProgressMode.Shared;

        /// <summary>
        /// Loot of a kill goes straight into the killer's backpack; off: it is only announced
        /// (<c>LootDroppedEvent</c>) and the game hands it to whoever picks it up with <see cref="ActorWorld.GiveItem(int, string, int)"/>.
        /// </summary>
        public bool LootToBackpack { get; set; } = true;

        /// <summary>
        /// Ids that are money, not items: a loot table entry or a quest reward with one of these ids pays into the
        /// player's wallet (<see cref="ActorWorld.Wallet"/>), and the entry's quantity range is the amount. Gold by default.
        /// </summary>
        public HashSet<string> Currencies { get; } = new HashSet<string> { RPGFramework.Trading.Currencies.Gold };

        /// <summary>
        /// Where players wear items (a head, two weapon hands, two rings): <see cref="ActorWorld.Gear"/>. Empty: the
        /// game has no gear.
        /// </summary>
        public List<GearSlot> GearSlots { get; } = new List<GearSlot>();

        /// <summary>
        /// Damage elements by attack tag bit ("fire" = 1 &lt;&lt; 3): a hit whose <see cref="RPGFramework.Combat.AttackContext.Tags"/>
        /// has the bit is of that element, and the target's resistances (<see cref="CharacterData.Resistances"/>, gear
        /// bonuses "resist:fire") change it (<see cref="ActorWorld.Resistances"/>). Several elements share the damage.
        /// </summary>
        public Dictionary<string, int> ElementTags { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// The chance (0..1) that a spawned enemy is elite (<see cref="ActionRpgContent.EliteAffixes"/>).
        /// </summary>
        public float EliteChance { get; set; }

        /// <summary>
        /// How many affixes an elite rolls, as the rarity's count in the pool (Rare: 2 by default).
        /// </summary>
        public RPGFramework.Items.ItemRarity EliteRarity { get; set; } = RPGFramework.Items.ItemRarity.Rare;

        /// <summary>
        /// An elite's experience is its character's times this; its loot table is rolled this many times (rounded).
        /// </summary>
        public float EliteRewardMultiplier { get; set; } = 2f;

        /// <summary>
        /// Skill points a player has at level 1 (<see cref="ActorSkills"/>).
        /// </summary>
        public int StartingSkillPoints { get; set; }

        /// <summary>
        /// Skill points every level above 1 gives.
        /// </summary>
        public int SkillPointsPerLevel { get; set; } = 1;

        /// <summary>
        /// The level enemies spawn at: 1 (the default) or the player's level, plus <see cref="EnemyLevelOffset"/>.
        /// Their stats grow by their character's per-level growth, their experience by <see cref="CharacterData.ExperiencePerLevel"/>.
        /// </summary>
        public EnemyLevelMode EnemyLevels { get; set; } = EnemyLevelMode.One;

        /// <summary>
        /// Levels added to (or, negative, taken from) the enemies' level; they are never below 1.
        /// </summary>
        public int EnemyLevelOffset { get; set; }

        /// <summary>
        /// Fixed random seed (loot, wave composition); 0 picks a new one every run.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Makes the game a PvP match (teams, score, respawns, Duel rounds); null = co-op, players never fight.
        /// </summary>
        public PvpSettings Pvp { get; set; }

        /// <summary>
        /// Who may attack whom; null uses <see cref="HostilityRules.Teams"/> in PvP and
        /// <see cref="HostilityRules.Coop"/> otherwise. Set a <see cref="FactionHostility"/> for teams that are factions.
        /// </summary>
        public IHostilityRules Hostility { get; set; }

        /// <summary>
        /// How much a hit hurts; null uses the registered <see cref="IDamageCalculator"/> (the framework's
        /// <see cref="Combat.DamageCalculator"/> unless a game module registers its own). Implement
        /// <see cref="IContextualDamageCalculator"/> as well to see hit tags and effect damage.
        /// </summary>
        public IDamageCalculator DamageCalculator { get; set; }

        /// <summary>
        /// Characters can have status effects (<see cref="ActorWorld.Effects"/>); also on when the content has any.
        /// </summary>
        public bool UseStatusEffects { get; set; }

        /// <summary>
        /// Characters can use abilities (<see cref="ActorWorld.Abilities"/>); also on when the content has any.
        /// </summary>
        public bool UseAbilities { get; set; }

        public bool UsesStatusEffects(ActionRpgContent content) => UseStatusEffects || (content?.StatusEffects.Count ?? 0) > 0;

        public bool UsesAbilities(ActionRpgContent content) => UseAbilities || (content?.Abilities.Count ?? 0) > 0;

        public IEnumerable<CharacterData> Characters => Player != null ? Enemies.Prepend(Player) : Enemies;

        public CharacterData FindCharacter(string id) => Characters.FirstOrDefault(c => c != null && c.Id == id);

        /// <summary>
        /// Mistakes that would break or silently empty the game, in words a designer understands.
        /// </summary>
        public List<string> Validate(ActionRpgContent content)
        {
            content = content ?? new ActionRpgContent();
            var problems = new List<string>();

            if (Player == null)
                problems.Add("No player character.");
            if (Waves != null && Waves.Enabled && Enemies.Count(e => e != null) == 0)
                problems.Add("Waves are on but there are no enemies.");
            if (Pvp != null)
                problems.AddRange(Pvp.Validate());

            var characters = Characters.Where(c => c != null).ToList();
            foreach (var blank in characters.Where(c => string.IsNullOrWhiteSpace(c.Id)))
                problems.Add($"Character '{blank.DisplayName}' has no id.");
            foreach (var duplicate in characters.GroupBy(c => c.Id).Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key)))
                problems.Add($"Several characters use the id '{duplicate.Key}'.");
            foreach (var character in characters.Where(c => c.MaxHealth <= 0f))
                problems.Add($"{character.DisplayName}: Max Health must be above 0.");

            var items = new HashSet<string>(content.Items.Where(i => i != null).Select(i => i.ItemId));
            var tables = new HashSet<string>(content.LootTables.Where(t => t != null).Select(t => t.TableId));
            var quests = content.Quests.Where(q => q != null).ToList();
            var questIds = new HashSet<string>(quests.Select(q => q.Id));

            foreach (var character in characters.Where(c => !string.IsNullOrEmpty(c.LootTableId) && !tables.Contains(c.LootTableId)))
                problems.Add($"{character.DisplayName}: loot table '{character.LootTableId}' is not in the setup.");

            foreach (var table in content.LootTables.Where(t => t != null))
            {
                var dropped = table.Entries.Select(e => e.ItemId).Concat(table.GuaranteedDrops ?? Enumerable.Empty<string>());
                foreach (var missing in dropped.Where(id => !items.Contains(id) && !Currencies.Contains(id)).Distinct())
                    problems.Add($"Loot table '{table.TableId}' drops '{missing}', which is not an item or a currency in the setup.");
            }

            var slots = GearSlots.Where(s => s != null).ToList();
            foreach (var blank in slots.Where(s => string.IsNullOrWhiteSpace(s.Id)))
                problems.Add($"A gear slot ('{blank.DisplayName}') has no id.");
            foreach (var duplicate in slots.GroupBy(s => s.Id).Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key)))
                problems.Add($"Several gear slots use the id '{duplicate.Key}'.");
            foreach (var gear in content.Gear.Where(g => g != null))
            {
                if (!items.Contains(gear.ItemId))
                    problems.Add($"Gear '{gear.ItemId}' is not an item in the setup.");
                else if (!slots.Any(s => s.Takes(gear)))
                    problems.Add($"No gear slot takes '{gear.ItemId}' (kind '{gear.Kind}').");
            }

            foreach (var quest in StartingQuests.Where(id => !questIds.Contains(id)))
                problems.Add($"Starting quest '{quest}' is not in the setup.");

            var characterIds = new HashSet<string>(characters.Select(c => c.Id));
            foreach (var quest in quests)
            {
                foreach (var objective in quest.Objectives.Where(o => o.Type == ObjectiveType.Kill && !string.IsNullOrEmpty(o.TargetId)))
                {
                    if (!characterIds.Contains(objective.TargetId))
                        problems.Add($"Quest '{quest.Title}' asks to kill '{objective.TargetId}', but no character has that id.");
                }
                foreach (var reward in quest.Rewards.Where(r => r.Type == RewardType.Item && !items.Contains(r.RewardId)))
                    problems.Add($"Quest '{quest.Title}' rewards '{reward.RewardId}', which is not an item in the setup.");
            }
            return problems;
        }
    }

    /// <summary>
    /// Whose level, experience, backpack and quests a co-op player plays with.
    /// </summary>
    public enum ProgressMode
    {
        /// <summary>
        /// The party shares the first player's (the host's): everyone's deeds count for it.
        /// </summary>
        Shared = 0,

        /// <summary>
        /// Each player has their own: brought along when joining and taken home.
        /// </summary>
        PerPlayer = 1
    }

    /// <summary>
    /// Which level enemies spawn at (<see cref="ActionRpgSettings.EnemyLevels"/>).
    /// </summary>
    public enum EnemyLevelMode
    {
        /// <summary>Level 1 (plus the offset).</summary>
        One = 0,

        /// <summary>The player's level (the host's in co-op), plus the offset.</summary>
        PlayerLevel = 1
    }

    /// <summary>
    /// The game's content in runtime form: registered into the framework registries when the game starts.
    /// </summary>
    public sealed class ActionRpgContent
    {
        public List<IItem> Items { get; } = new List<IItem>();
        public List<ILootTable> LootTables { get; } = new List<ILootTable>();
        public List<IQuest> Quests { get; } = new List<IQuest>();

        /// <summary>
        /// Status effects the game knows (poison, stun, a speed boost); registered when the game starts.
        /// </summary>
        public List<StatusEffectData> StatusEffects { get; } = new List<StatusEffectData>();

        /// <summary>
        /// Abilities the game knows; what each does is set with <see cref="ActorAbilities.SetEffect"/>.
        /// </summary>
        public List<IAbility> Abilities { get; } = new List<IAbility>();

        /// <summary>
        /// Items that can be worn: the slot kind that takes them, their level and bonuses
        /// (<see cref="ActionRpgSettings.GearSlots"/>).
        /// </summary>
        public List<GearData> Gear { get; } = new List<GearData>();

        /// <summary>
        /// Random affixes for gear that drops as loot ("Sturdy Sword of the Bear"): each piece of gear from a loot
        /// table rolls a rarity (<see cref="Rarities"/>) and that rarity's affixes, with the gear's kind as the item's
        /// kind for <see cref="AffixDefinition.OnlyOn"/> and the player's level as the item level. The rolled item's id
        /// carries them (<see cref="RolledItems"/>), so backpacks, saves and co-op keep them. Null: gear drops as written.
        /// </summary>
        public AffixPool Affixes { get; set; }

        /// <summary>
        /// How often dropped gear is of each rarity; null: <see cref="RarityTable.Default"/>.
        /// </summary>
        public RarityTable Rarities { get; set; }

        /// <summary>
        /// A player's luck (magic find) for <see cref="RarityTable.Roll"/>, by player id; null: no luck.
        /// </summary>
        public Func<int, float> LuckOf { get; set; }

        /// <summary>
        /// Items that belong together: wearing several pieces of a set gives its bonuses for that many pieces
        /// (stats, or "resist:fire"). Rolled gear counts as its base item.
        /// </summary>
        public ItemSets ItemSets { get; } = new ItemSets();

        /// <summary>
        /// The skills players learn with the points their levels give (<see cref="ActorWorld.Skills"/>); bonuses are
        /// <see cref="ActorStat"/> names or "resist:fire". Null: no skill tree.
        /// </summary>
        public RPGFramework.Skills.ISkillTree SkillTree { get; set; }

        /// <summary>
        /// What players can craft from their backpacks (<see cref="ActorWorld.Crafting"/>).
        /// </summary>
        public List<RPGFramework.Crafting.ICraftingRecipe> Recipes { get; } = new List<RPGFramework.Crafting.ICraftingRecipe>();

        /// <summary>
        /// The game's achievements: kills by character id (KillCount), items by base item id (ItemCollected),
        /// completed quests (QuestCompleted), the level (LevelReached) and strike damage (DamageDealt) move them;
        /// a criterion with no target counts every one (<see cref="ActorWorld.Achievements"/>).
        /// </summary>
        public List<RPGFramework.Achievements.Runtime.Contracts.IAchievement> Achievements { get; } =
            new List<RPGFramework.Achievements.Runtime.Contracts.IAchievement>();

        /// <summary>
        /// The game's conversations (<see cref="ActorWorld.Dialogue"/>).
        /// </summary>
        public List<RPGFramework.Dialogue.IDialogue> Dialogues { get; } = new List<RPGFramework.Dialogue.IDialogue>();

        /// <summary>
        /// What dialogue lines and choices do: items, currency, quests, variables, skill points.
        /// </summary>
        public List<DialogueEffect> DialogueEffects { get; } = new List<DialogueEffect>();

        /// <summary>
        /// Affixes an elite enemy can roll ("Frenzied": +50% Attack, "of Stone": +10 Defense): stats are
        /// <see cref="ActorStat"/> names (or "resist:fire"); an enemy spawns elite with
        /// <see cref="ActionRpgSettings.EliteChance"/> and rolls them at the player's level and
        /// <see cref="ActionRpgSettings.EliteRarity"/>, the character's id as the affix kind. Null: no elites.
        /// </summary>
        public AffixPool EliteAffixes { get; set; }

        private readonly Dictionary<string, IItem> _rolledItems = new Dictionary<string, IItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, GearData> _rolledGear = new Dictionary<string, GearData>(StringComparer.Ordinal);

        /// <summary>
        /// An item of the content by id, rolled ones included (built from their base item and <see cref="Affixes"/>).
        /// </summary>
        public IItem FindItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            if (!RolledItems.IsRolled(itemId))
                return Items.FirstOrDefault(i => i != null && i.ItemId == itemId);
            if (_rolledItems.TryGetValue(itemId, out var rolled))
                return rolled;
            rolled = RolledItems.Create(itemId, FindItem(RolledItems.BaseOf(itemId)), Affixes);
            if (rolled != null)
                _rolledItems[itemId] = rolled;
            return rolled;
        }

        /// <summary>
        /// The gear an item is, rolled ones included: the base gear's kind, level and bonuses plus a bonus per affix.
        /// </summary>
        public GearData FindGear(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            if (!RolledItems.IsRolled(itemId))
                return Gear.FirstOrDefault(g => g != null && g.ItemId == itemId);
            if (_rolledGear.TryGetValue(itemId, out var rolled))
                return rolled;
            var baseGear = FindGear(RolledItems.BaseOf(itemId));
            if (baseGear == null || !RolledItems.TryParse(itemId, out _, out _, out var affixes))
                return null;
            rolled = new GearData(itemId, baseGear.Kind, baseGear.Bonuses.ToArray()) { MinimumLevel = baseGear.MinimumLevel };
            foreach (var affix in affixes)
            {
                var definition = Affixes?.Find(affix.AffixId);
                if (definition != null)
                    rolled.Bonuses.Add(new GearBonus(definition.Stat, affix.Value, definition.ModifierType));
            }
            _rolledGear[itemId] = rolled;
            return rolled;
        }
    }
}
