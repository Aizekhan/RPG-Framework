using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Items;
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
        /// Fixed random seed (loot, wave composition); 0 picks a new one every run.
        /// </summary>
        public int Seed { get; set; }

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
                foreach (var missing in dropped.Where(id => !items.Contains(id)).Distinct())
                    problems.Add($"Loot table '{table.TableId}' drops '{missing}', which is not an item in the setup.");
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
    /// The game's content in runtime form: registered into the framework registries when the game starts.
    /// </summary>
    public sealed class ActionRpgContent
    {
        public List<IItem> Items { get; } = new List<IItem>();
        public List<ILootTable> LootTables { get; } = new List<ILootTable>();
        public List<IQuest> Quests { get; } = new List<IQuest>();
    }
}
