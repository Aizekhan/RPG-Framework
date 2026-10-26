using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RPGFramework.Spawning
{
    /// <summary>
    /// Designer-edited list of spawnable prefabs: which key maps to which prefab (or is loaded by the asset manager)
    /// and which keys are pooled. Apply it to an <see cref="IPrefabSpawner"/> at startup.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Spawning/Prefab Catalog", fileName = "PrefabCatalog")]
    public sealed class PrefabCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Key passed to IPrefabSpawner.SpawnAsync")]
            public string Key;

            [Tooltip("Direct reference; leave empty to load the key through the asset manager (Resources, Addressables)")]
            public GameObject Prefab;

            [Tooltip("Recycle instances instead of instantiating and destroying them")]
            public bool Pooled;

            [Min(0)] public int PrewarmCount;

            [Min(1)] public int MaxPoolSize = 64;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        public static PrefabCatalog Create(IEnumerable<Entry> entries)
        {
            var catalog = CreateInstance<PrefabCatalog>();
            catalog._entries.AddRange(entries);
            return catalog;
        }

        /// <summary>
        /// Registers direct prefabs and pool settings. Entries without a key are skipped.
        /// </summary>
        public void ApplyTo(IPrefabSpawner spawner)
        {
            if (spawner == null)
                throw new ArgumentNullException(nameof(spawner));

            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                    continue;
                if (entry.Prefab != null)
                    spawner.Register(entry.Key, entry.Prefab);
                if (entry.Pooled)
                    spawner.ConfigurePool(entry.Key, entry.PrewarmCount, Math.Max(1, entry.MaxPoolSize));
            }
        }

        /// <summary>
        /// Creates the pools of pooled entries with a prewarm count (e.g. behind a loading screen).
        /// </summary>
        public async UniTask PrewarmAsync(IPrefabSpawner spawner, CancellationToken cancellationToken = default)
        {
            if (spawner == null)
                throw new ArgumentNullException(nameof(spawner));

            foreach (var entry in _entries)
            {
                if (entry != null && entry.Pooled && entry.PrewarmCount > 0 && !string.IsNullOrEmpty(entry.Key))
                    await spawner.PrewarmAsync(entry.Key, cancellationToken);
            }
        }
    }
}
