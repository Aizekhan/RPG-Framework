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
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Keeps the scene in step with the <see cref="ActorWorld"/>: a character that spawns gets a view from its
    /// <see cref="CharacterDefinition"/> prefab (or a placeholder capsule), a character that leaves loses it.
    /// Any prefab works: player movement, enemy AI, a strike animation and a health bar are added when missing.
    /// </summary>
    public sealed class ActorPresenter : IDisposable
    {
        private const string Category = "ActionRpg";

        private readonly ActorWorld _world;
        private readonly GameSetup _setup;
        private readonly IEventBus _bus;
        private readonly IPrefabSpawner _spawner;
        private readonly IEntityViewRegistry _views;
        private readonly Transform _root;
        private readonly ILogger _logger;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly HashSet<GameObject> _placeholders = new HashSet<GameObject>();

        public ActorPresenter(ActorWorld world, GameSetup setup, IEventBus bus, IPrefabSpawner spawner,
            IEntityViewRegistry views, Transform root, ILogger logger)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _root = root;
            _logger = logger;
        }

        /// <summary>
        /// Raised when a view is in the scene and bound to its entity.
        /// </summary>
        public event Action<int, GameObject> ViewShown;

        public IEntityViewRegistry Views => _views;

        /// <summary>
        /// Registers the prefabs, shows everyone already in the world (the player spawns while the game starts)
        /// and follows from then on.
        /// </summary>
        public void Start()
        {
            foreach (var character in _setup.Characters)
            {
                if (character.Prefab != null)
                    _spawner.Register(character.Id, character.Prefab);
                if (!character.Equals(_setup.Player) && character.Prefab != null)
                    _spawner.ConfigurePool(character.Id);
            }

            _bus.Subscribe<ActorSpawnedEvent>(e => ShowAsync(e.EntityId).Forget()).AddTo(_subscriptions);
            _bus.Subscribe<ActorDespawnedEvent>(e => Hide(e.EntityId)).AddTo(_subscriptions);
            foreach (var actor in new List<Actor>(_world.Actors))
                ShowAsync(actor.EntityId).Forget();
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

        private async UniTaskVoid ShowAsync(int entityId)
        {
            var actor = _world.Get(entityId);
            if (actor == null || !_pending.Add(entityId))
                return;

            var definition = _setup.FindCharacter(actor.CharacterId);
            if (definition == null)
            {
                _pending.Remove(entityId);
                _logger?.LogError($"No character '{actor.CharacterId}' in the Game Setup", Category);
                return;
            }

            GameObject instance;
            try
            {
                var position = PickPosition(actor);
                instance = definition.Prefab != null
                    ? await _spawner.SpawnAsync(definition.Id, position, Quaternion.identity, _root, _lifetime.Token)
                    : CreatePlaceholder(definition, position);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger?.LogError($"No view for '{definition.Id}' (entity {entityId}): {exception.Message}", Category);
                return;
            }
            finally
            {
                _pending.Remove(entityId);
            }

            // Gone while the prefab was loading
            if (_lifetime.IsCancellationRequested || _world.Get(entityId) == null)
            {
                Destroy(instance);
                return;
            }

            instance.name = $"{definition.DisplayName} #{entityId}";
            AddMissingParts(instance, actor.IsPlayer);
            _views.Bind(entityId, instance);
            foreach (var part in instance.GetComponentsInChildren<ActorView>(true))
                part.Bind(_world, _views, definition, entityId);
            ViewShown?.Invoke(entityId, instance);
        }

        private void Hide(int entityId)
        {
            var instance = _views.Unbind(entityId);
            if (instance == null)
                return;
            foreach (var part in instance.GetComponentsInChildren<ActorView>(true))
                part.Unbind();
            Destroy(instance);
        }

        private void Destroy(GameObject instance)
        {
            if (_placeholders.Remove(instance))
                Object.Destroy(instance);
            else
                _spawner.Despawn(instance);
        }

        private static void AddMissingParts(GameObject instance, bool isPlayer)
        {
            if (instance.GetComponent<StrikeAnimator>() == null)
                instance.AddComponent<StrikeAnimator>();

            if (isPlayer && instance.GetComponent<PlayerController>() == null)
            {
                if (instance.GetComponent<CharacterController>() == null)
                {
                    var body = instance.AddComponent<CharacterController>();
                    body.center = new Vector3(0f, 1f, 0f);
                    body.height = 2f;
                    body.radius = 0.4f;
                }
                instance.AddComponent<PlayerController>();
            }
            else if (!isPlayer && instance.GetComponent<EnemyBrain>() == null)
            {
                instance.AddComponent<EnemyBrain>();
            }

            if (instance.GetComponentInChildren<HealthBarView>(true) == null)
                instance.AddComponent<HealthBarView>();
        }

        private GameObject CreatePlaceholder(CharacterDefinition definition, Vector3 position)
        {
            var root = new GameObject(definition.DisplayName);
            root.transform.SetParent(_root, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.GetComponent<Renderer>().material.color = definition.PlaceholderColor;

            // A nose, so you can see where it faces
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(body.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.45f, 0.45f);
            nose.transform.localScale = new Vector3(0.25f, 0.2f, 0.3f);
            nose.GetComponent<Renderer>().material.color = definition.PlaceholderColor * 0.6f;

            _placeholders.Add(root);
            return root;
        }

        private Vector3 PickPosition(Actor actor)
        {
            var origin = _root != null ? _root.position : Vector3.zero;
            if (actor.IsPlayer)
                return origin;
            var centre = _views.TryGetView(_world.PlayerId, out var player) ? player.transform.position : origin;
            var onCircle = Random.insideUnitCircle.normalized * _setup.SpawnRadius;
            return new Vector3(centre.x + onCircle.x, origin.y, centre.z + onCircle.y);
        }
    }
}
