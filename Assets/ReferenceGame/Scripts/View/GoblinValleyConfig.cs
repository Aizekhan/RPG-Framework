using RPGFramework.Spawning;
using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Everything a designer tunes in Goblin Valley without code: the rules (waves, seed) and which prefab shows
    /// each kind of combatant. Prefab keys in the catalog are the combatant kinds: "player" and "goblin".
    /// </summary>
    [CreateAssetMenu(menuName = "Goblin Valley/Game Config", fileName = "GoblinValleyConfig")]
    public sealed class GoblinValleyConfig : ScriptableObject
    {
        [SerializeField] private GoblinValleyOptions _rules = new GoblinValleyOptions();

        [Tooltip("Prefab per combatant kind (keys \"player\" and \"goblin\")")]
        [SerializeField] private PrefabCatalog _prefabs;

        [Tooltip("Goblins appear on a circle of this radius around the hero")]
        [SerializeField, Min(1f)] private float _spawnRadius = 9f;

        public GoblinValleyOptions Rules => _rules;
        public PrefabCatalog Prefabs => _prefabs;
        public float SpawnRadius => _spawnRadius;

        public static GoblinValleyConfig Create(PrefabCatalog prefabs, GoblinValleyOptions rules = null)
        {
            var config = CreateInstance<GoblinValleyConfig>();
            config._prefabs = prefabs;
            config._rules = rules ?? new GoblinValleyOptions();
            return config;
        }
    }
}
