using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Presentation;
using RPGFramework.Spawning;
using UnityEngine;
using ILogger = RPGFramework.Logging.ILogger;
using Random = UnityEngine.Random;

namespace ReferenceGame
{
    /// <summary>
    /// Keeps the scene in step with the game: a combatant spawned in the game gets a view from the prefab
    /// spawner (the prefab key is its kind), a despawned one loses it. Views are linked to their entities in the
    /// <see cref="IEntityViewRegistry"/>.
    /// </summary>
    public sealed class GoblinValleyPresenter : IDisposable
    {
        private const string Category = "GoblinValley";

        private readonly GoblinValleyGame _game;
        private readonly IEventBus _bus;
        private readonly IPrefabSpawner _spawner;
        private readonly IEntityViewRegistry _views;
        private readonly Transform _root;
        private readonly float _spawnRadius;
        private readonly ILogger _logger;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly HashSet<int> _pending = new HashSet<int>();

        public GoblinValleyPresenter(GoblinValleyGame game, IEventBus bus, IPrefabSpawner spawner, IEntityViewRegistry views,
            Transform root, float spawnRadius, ILogger logger)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _root = root;
            _spawnRadius = spawnRadius;
            _logger = logger;
        }

        /// <summary>
        /// Raised when a view is in the scene and bound to its entity.
        /// </summary>
        public event Action<int, GameObject> ViewShown;

        public IEntityViewRegistry Views => _views;

        /// <summary>
        /// Shows everyone already in the valley (the hero spawns while the game starts) and follows from then on.
        /// </summary>
        public void Start()
        {
            _bus.Subscribe<CombatantSpawnedEvent>(e => ShowAsync(e.EntityId, e.Kind).Forget()).AddTo(_subscriptions);
            _bus.Subscribe<CombatantDespawnedEvent>(e => Hide(e.EntityId)).AddTo(_subscriptions);
            foreach (var combatant in new List<Combatant>(_game.Combatants))
                ShowAsync(combatant.EntityId, combatant.Kind).Forget();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            foreach (var view in new List<EntityView>(_views.Views))
            {
                if (view != null)
                    Hide(view.EntityId);
            }
        }

        private async UniTaskVoid ShowAsync(int entityId, string kind)
        {
            if (!_pending.Add(entityId))
                return;

            GameObject instance;
            try
            {
                instance = await _spawner.SpawnAsync(kind, PickPosition(kind), Quaternion.identity, _root, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger?.LogError($"No view for '{kind}' (entity {entityId}): {exception.Message}", Category);
                return;
            }
            finally
            {
                _pending.Remove(entityId);
            }

            // Gone while the prefab was loading
            if (_lifetime.IsCancellationRequested || _game.Get(entityId) == null)
            {
                _spawner.Despawn(instance);
                return;
            }

            _views.Bind(entityId, instance);
            foreach (var part in instance.GetComponentsInChildren<CombatantView>(true))
                part.Bind(_game, _views, entityId);
            ViewShown?.Invoke(entityId, instance);
        }

        private void Hide(int entityId)
        {
            var instance = _views.Unbind(entityId);
            if (instance == null)
                return;
            foreach (var part in instance.GetComponentsInChildren<CombatantView>(true))
                part.Unbind();
            _spawner.Despawn(instance);
        }

        private Vector3 PickPosition(string kind)
        {
            var centre = _views.TryGetView(_game.PlayerId, out var hero) ? hero.transform.position : Vector3.zero;
            if (kind == GoblinValleyContent.PlayerKind)
                return Vector3.zero;
            var onCircle = Random.insideUnitCircle.normalized * _spawnRadius;
            return new Vector3(centre.x + onCircle.x, 0f, centre.z + onCircle.y);
        }
    }
}
