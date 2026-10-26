using System.Collections.Generic;
using System.Linq;
using RPGFramework.Quests;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The whole game in one asset: the player, the enemies, how waves come, items, loot and quests.
    /// Drop it on an <see cref="ActionRpgHost"/> in the scene and press Play.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Game Setup", fileName = "GameSetup", order = 0)]
    public sealed class GameSetup : ScriptableObject
    {
        [Header("Characters")]
        [SerializeField] private CharacterDefinition _player;
        [SerializeField] private List<CharacterDefinition> _enemies = new List<CharacterDefinition>();

        [Header("Waves")]
        [SerializeField] private WaveSettings _waves = new WaveSettings();

        [Tooltip("Enemies appear on a circle of this radius around the player")]
        [SerializeField, Min(1f)] private float _spawnRadius = 9f;

        [Header("Content")]
        [Tooltip("Every item that can drop or be rewarded")]
        [SerializeField] private List<ItemAsset> _items = new List<ItemAsset>();

        [Tooltip("Loot tables of the characters are added automatically; list others here")]
        [SerializeField] private List<LootTableAsset> _lootTables = new List<LootTableAsset>();

        [Tooltip("The player has these quests from the start")]
        [SerializeField] private List<QuestAsset> _startingQuests = new List<QuestAsset>();

        [Tooltip("Other quests (given later by your own code)")]
        [SerializeField] private List<QuestAsset> _otherQuests = new List<QuestAsset>();

        [Header("Rules")]
        [Tooltip("Turn a quest in as soon as its objectives are done")]
        [SerializeField] private bool _completeQuestsAutomatically = true;

        [SerializeField, Min(1)] private int _inventoryCapacity = 40;

        [Tooltip("0 = different every run; any other number repeats loot and waves exactly")]
        [SerializeField] private int _seed;

        [Header("Camera")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 8f, -6.5f);

        public CharacterDefinition Player => _player;
        public IReadOnlyList<CharacterDefinition> Enemies => _enemies;
        public WaveSettings Waves => _waves;
        public float SpawnRadius => _spawnRadius;
        public Vector3 CameraOffset => _cameraOffset;
        public List<ItemAsset> Items => _items;
        public List<LootTableAsset> LootTables => _lootTables;
        public List<QuestAsset> StartingQuests => _startingQuests;
        public List<QuestAsset> OtherQuests => _otherQuests;

        public IEnumerable<CharacterDefinition> Characters =>
            (_player != null ? new[] { _player } : Enumerable.Empty<CharacterDefinition>()).Concat(_enemies.Where(e => e != null));

        public CharacterDefinition FindCharacter(string id) => Characters.FirstOrDefault(c => c.Id == id);

        public static GameSetup Create(CharacterDefinition player, IEnumerable<CharacterDefinition> enemies, WaveSettings waves = null)
        {
            var setup = CreateInstance<GameSetup>();
            setup._player = player;
            setup._enemies.AddRange(enemies ?? Enumerable.Empty<CharacterDefinition>());
            setup._waves = waves ?? new WaveSettings();
            return setup;
        }

        public void SetPlayer(CharacterDefinition player) => _player = player;

        public void AddEnemy(CharacterDefinition enemy)
        {
            if (enemy != null && !_enemies.Contains(enemy))
                _enemies.Add(enemy);
        }

        public void SetSeed(int seed) => _seed = seed;

        public ActionRpgSettings BuildSettings()
        {
            var settings = new ActionRpgSettings
            {
                Player = _player != null ? _player.ToData() : null,
                Waves = _waves ?? new WaveSettings(),
                CompleteQuestsAutomatically = _completeQuestsAutomatically,
                InventoryCapacity = _inventoryCapacity,
                Seed = _seed
            };
            settings.Enemies.AddRange(_enemies.Where(e => e != null).Select(e => e.ToData()));
            settings.StartingQuests.AddRange(_startingQuests.Where(q => q != null).Select(q => q.questId));
            return settings;
        }

        public ActionRpgContent BuildContent()
        {
            var content = new ActionRpgContent();
            content.Items.AddRange(_items.Where(i => i != null).Distinct().Select(i => i.ToItem()));

            var tables = _lootTables.Concat(Characters.Select(c => c.Loot)).Where(t => t != null).Distinct();
            content.LootTables.AddRange(tables.Select(t => t.ToLootTable()));

            var quests = _startingQuests.Concat(_otherQuests).Where(q => q != null).Distinct();
            content.Quests.AddRange(quests.Select(q => (IQuest)new Quest(q.ToQuestData())));
            return content;
        }

        /// <summary>
        /// Everything that would break the game or leave it silently empty; empty when the setup is good to play.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (_enemies.Any(e => e == null))
                problems.Add("The enemy list has an empty slot.");
            if (_items.Any(i => i == null))
                problems.Add("The item list has an empty slot.");
            if (_startingQuests.Any(q => q == null) || _otherQuests.Any(q => q == null))
                problems.Add("A quest list has an empty slot.");
            if (_player != null && _enemies.Contains(_player))
                problems.Add($"{_player.DisplayName} is both the player and an enemy.");

            problems.AddRange(BuildSettings().Validate(BuildContent()));
            return problems;
        }
    }
}
