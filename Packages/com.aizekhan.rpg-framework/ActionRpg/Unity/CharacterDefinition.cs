using RPGFramework.Tools.LootTableDesigner;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// One kind of character (the hero, a goblin, a wolf): how it looks, how strong it is and what it drops.
    /// Put it in a <see cref="GameSetup"/> as the player or as an enemy.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Character", fileName = "NewCharacter", order = 1)]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Tooltip("Unique id; Kill quest objectives use it as their target (\"goblin\")")]
        [SerializeField] private string _id = "";

        [SerializeField] private string _displayName = "";

        [Tooltip("The model shown in the game. Leave empty for a placeholder capsule. Movement, AI and a health bar " +
                 "are added automatically if the prefab has none")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Placeholder colour when there is no prefab")]
        [SerializeField] private Color _placeholderColor = new Color(0.8f, 0.3f, 0.25f);

        [Header("Stats at level 1")]
        [SerializeField, Min(1f)] private float _maxHealth = 30f;
        [SerializeField, Min(0f)] private float _attack = 6f;
        [SerializeField, Min(0f)] private float _defense = 2f;

        [Header("Growth per level (player)")]
        [SerializeField, Min(0f)] private float _healthPerLevel = 20f;
        [SerializeField, Min(0f)] private float _attackPerLevel = 3f;
        [SerializeField, Min(0f)] private float _defensePerLevel = 2f;

        [Header("Movement and combat")]
        [SerializeField, Min(0f)] private float _moveSpeed = 3.2f;
        [Tooltip("How far (meters) its attacks reach")]
        [SerializeField, Min(0.1f)] private float _attackRange = 1.6f;
        [Tooltip("Seconds between attacks")]
        [SerializeField, Min(0.05f)] private float _attackCooldown = 1.2f;

        [Header("When killed")]
        [SerializeField, Min(0)] private long _experience = 40;

        [Tooltip("Rolled for the killer; add the dropped items to the Game Setup")]
        [SerializeField] private LootTableAsset _loot;

        public string Id => string.IsNullOrWhiteSpace(_id) ? name.ToLowerInvariant().Replace(' ', '_') : _id;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public GameObject Prefab => _prefab;
        public Color PlaceholderColor => _placeholderColor;
        public float MoveSpeed => _moveSpeed;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public LootTableAsset Loot => _loot;

        public CharacterData ToData() => new CharacterData
        {
            Id = Id,
            DisplayName = DisplayName,
            MaxHealth = _maxHealth,
            Attack = _attack,
            Defense = _defense,
            HealthPerLevel = _healthPerLevel,
            AttackPerLevel = _attackPerLevel,
            DefensePerLevel = _defensePerLevel,
            ExperienceReward = _experience,
            LootTableId = _loot != null ? _loot.tableId : ""
        };

        /// <summary>
        /// Sets up a definition in code (tests, generators).
        /// </summary>
        public static CharacterDefinition Create(string id, string displayName, float maxHealth, float attack, float defense,
            long experience = 0, GameObject prefab = null, LootTableAsset loot = null)
        {
            var definition = CreateInstance<CharacterDefinition>();
            definition.name = displayName;
            definition._id = id;
            definition._displayName = displayName;
            definition._maxHealth = maxHealth;
            definition._attack = attack;
            definition._defense = defense;
            definition._experience = experience;
            definition._prefab = prefab;
            definition._loot = loot;
            return definition;
        }

        public void SetMovement(float moveSpeed, float attackRange, float attackCooldown)
        {
            _moveSpeed = moveSpeed;
            _attackRange = attackRange;
            _attackCooldown = attackCooldown;
        }

        public void SetGrowth(float healthPerLevel, float attackPerLevel, float defensePerLevel)
        {
            _healthPerLevel = healthPerLevel;
            _attackPerLevel = attackPerLevel;
            _defensePerLevel = defensePerLevel;
        }

        public void SetPlaceholderColor(Color color) => _placeholderColor = color;

        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(_id))
                _id = _id.Trim().ToLowerInvariant().Replace(' ', '_');
        }
    }
}
