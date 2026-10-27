# Recipe: core services — use the framework's, never write your own

The framework already has the plumbing every game needs. **Before writing a pool, a loader, a timer, a state machine,
a path search or a logger, find it here.** Architecture rule GA13 fails on the usual hand-made versions
(`Resources.Load`, `SceneManager.LoadScene`, `Debug.Log`, `Invoke`, `WaitForSeconds`, `Instantiate`/`Destroy`,
`Stack<GameObject>` pools); GA11 fails on a type named like a framework type.

## Where the services come from

`GameHost` builds a DI container. Two modules fill it with shared singletons:

- `FrameworkCoreModule` (always): `ILogger`, `IEventBus`/`IEventPublisher`, `ITimerService`, `IEntityManager`,
  `ISystemRegistry`, `IWorld`, `PhaseTracker`/`IPhaseProvider`.
- `UnityServicesModule` (`ActionRpgHost` adds it; a plain `GameHostBehaviour` adds it with
  `builder.UseModule(new UnityServicesModule())`): `IPoolManager`, `IAssetManager` (Resources), `IPrefabSpawner`,
  `ISceneDispatcher`. On shutdown it releases pools and unloads assets.

Get them like this:

| Code | How |
|---|---|
| Rules and systems in `<Game>.Core` | Constructor parameters; the module registers them: `Container.RegisterFactory(c => new Shop(c.Resolve<IEventBus>(), c.Resolve<ITimerService>()))`, or `AddSystem<T>()` |
| The host (`ActionRpgHost` subclass) | `Host.Container.Resolve<IPrefabSpawner>()`. Resolve again after a restart (joining co-op restarts the host); do not keep a service from the previous game |
| Views (MonoBehaviours) | The host or presenter hands them what they need when it binds them (`view.Bind(...)`); views never search for services |

Extra asset sources (Addressables, mod folders) go in the host:

```csharp
protected override void ConfigureUnityServices(UnityServicesOptions options) =>
    options.AddAssetProvider(() => new AddressablesAssetProvider());   // RPGFramework.AssetManagement.Addressables
```

In Unity code `ILogger` clashes with `UnityEngine.ILogger`: add `using ILogger = RPGFramework.Logging.ILogger;`.

## Core runtime

| Need | Use this | Not that |
|---|---|---|
| Log something | `ILogger`: `logger.LogInfo("Chest opened", "Loot")`, `LogWarning`, `LogError(msg, category, exception)` | `Debug.Log` (no levels, no file, no category filter) |
| Tell other features something happened | `IEventBus.Publish(new ChestOpenedEvent(id))`; listen with `bus.Subscribe<ChestOpenedEvent>(OnOpened).AddTo(_subscriptions)` (recipe 03). `Enqueue` delivers at the start of the next frame | Direct calls between features, C# events on singletons, `SendMessage` |
| Do something later or repeatedly | `ITimerService`: `var h = timers.Schedule(2f, Explode)`, `timers.Repeat(1f, Regenerate)`, `await timers.DelayAsync(0.5f, token)`; `timers.Cancel(h)`. Game time: `TimeScale`, `PauseCategory("combat")` | `Invoke`, `InvokeRepeating`, coroutines with `WaitForSeconds`, `Time.time` bookkeeping |
| Per-frame logic | A system: `class Regen : SystemBase, IUpdateSystem` (also `IFixedUpdateSystem`, `ILateUpdateSystem`), added with `AddSystem<Regen>(SystemPriorities.Gameplay)`; a one-liner: `new ActionSystem("spin", dt => ...)` (recipe 09) | `Update()` in a MonoBehaviour that holds rules |
| Logic only in some game phases (menu, combat, cutscene) | `PhaseTracker.SetPhase("combat")` (publishes `PhaseChangedEvent`); a system implements `IPhaseFilteredSystem.IsActiveInPhase`, or sits in `new PhaseSystemGroup("Combat", logger, "combat")` | `if (state == ...)` at the top of every Update |
| Entities and data | `IEntityManager`: `CreateEntity`, `AddComponent(id, new Health { Value = 10 })` (components are `struct : IComponent`), `TryGetComponent`, `ref GetComponentRef<T>(id)` | Dictionaries of entity data scattered over MonoBehaviours |
| Query many entities every frame | `entities.GetEntitiesWith<Position, Velocity>(_ids)` into a reused `List<int>`: no allocation (also `<T1>`, `<T1, T2, T3>`) | The array overloads in hot code, LINQ over all entities |
| Entity templates ("goblin" = Health + Position + Loot) | `ArchetypeCatalog`: `catalog.Define("goblin", a => a.With(new Health { Value = 30 }).With<Position>())`, `catalog.Spawn("goblin", entities)`; inheritance with `ArchetypeBuilder.Extends(parent)` | Hand-written factory methods per kind |
| The world that ticks it all | `IWorld` (`World`: `Entities` + `Systems`, `Time`, `FrameCount`), built by `FrameworkCoreModule` and ticked by `GameHostBehaviour` | A second update loop or manager MonoBehaviour |
| Services and features | A `GameFeatureModule` per feature: `OnConfigure` registers, `OnInitialize` starts, `OnShutdown` disposes; `Dependencies` names the modules it needs (recipe 02) | Singletons, `static Instance`, `FindObjectOfType` |
| Async work one at a time (saving, loading, a queue of transitions) | `AsyncSerialQueue.RunAsync(() => SaveAsync())`: a second request waits for the first | Flags like `_isBusy` and racing tasks |
| Fire-and-forget async work that must stop on shutdown | `IAsyncTaskTracker` (`AsyncTaskTracker`): `tracker.Track(ct => PollAsync(ct), "poll")`, `CancelAll()` / `Dispose()` in `OnShutdown` | `async void`, untracked `Forget()` |
| Argument checks | `Ensure.NotNull(value, nameof(value))`, `NotNullOrEmpty` | Hand-written guard helpers |
| Save the game | `SaveParticipant<TState>` + `ActionRpgSaves.Register` (recipe 12) | `PlayerPrefs`, hand-written JSON files |
| Versioned binary data (network messages, custom files) | `VersionedSerializer(new TypeRegistry(), migrations)`: `Serialize<T>(data)` / `Deserialize<T>(bytes)` for `ISerializable` types; old versions upgraded through `MigrationRegistry.RegisterMigration<TOld, TNew>(typeId, 1, 2, migrator)` | `BinaryFormatter`, ad-hoc version bytes |

```csharp
public sealed class Bomb : IDisposable
{
    private readonly ITimerService _timers;
    private readonly IEventBus _bus;
    private TimerHandle _fuse;

    public Bomb(ITimerService timers, IEventBus bus)
    {
        _timers = timers;
        _bus = bus;
    }

    public void Light(int entityId) => _fuse = _timers.Schedule(3f, () => _bus.Publish(new BombExplodedEvent(entityId)), "combat");

    public void Defuse() => _timers.Cancel(_fuse);

    public void Dispose() => _timers.Cancel(_fuse);
}
```

## Unity services

| Need | Use this | Not that |
|---|---|---|
| Spawn and remove things that come and go (enemies, projectiles, effects, loot drops) | `IPrefabSpawner`: `Register(key, prefab)` (or let it load the key through `IAssetManager`), `ConfigurePool(key, initialSize, maxSize)`, `await SpawnAsync(key, position, rotation, parent)`, `Despawn(instance)`. `IPoolable.OnSpawned`/`OnDespawned` on the prefab resets it | `Instantiate` / `Destroy` in gameplay code, a `Stack<GameObject>` |
| Pool plain C# objects (path buffers, damage results, messages) | `new ObjectPool<T>(() => new T(), onGet, onRelease, initialSize: 0, maxSize: 64)`, `Get()` / `Release(item)`; register it in `IPoolManager` (`pools.Register("paths", pool)`) to get leak checks and the F3 statistics | A `Stack<T>` you manage by hand |
| A prefab pool without the spawner | `pools.CreateGameObjectPool(key, prefab, initialSize, maxSize, owner: sceneName)`, `pools.Spawn(key, position, rotation)`; with a scene as owner it is removed when the scene unloads | Pools that outlive their scene |
| Load an asset (sprite, audio, ScriptableObject, prefab) | `IAssetManager`: `using var handle = await assets.LoadAsync<AudioClip>("Audio/Hit")` (or keep the handle and `Dispose` it when done); several at once: `PreloadAsync<T>(keys, progress)` returns a group to dispose; a one-off object: `await assets.InstantiateAsync(key, position, rotation)` (unloaded with it) | `Resources.Load`, direct `Addressables` calls scattered in views |
| Change scenes | `ISceneDispatcher`: `await scenes.LoadAsync("Town", payload)`, `LoadAdditiveAsync`, `UnloadAsync`, `TryGetPayload<T>`; loading screens: `AddTransitionHandler(handler)`; events `SceneOperationStarted/Completed/Failed` on the bus | `SceneManager.LoadScene`, static fields to pass data between scenes |
| Link an entity to its GameObject | `IEntityViewRegistry` (`EntityViewRegistry`): `Bind(entityId, view)`, `Unbind`, `TryGetView`, `TryGetEntity(gameObject, out id)` (recipe 04) | `GetComponent<EnemyId>()` lookups, `GameObject.Find` |
| A camera that follows | `CameraFollow` (`ICameraController`): `SetTarget(transform)`, `Shake(intensity, duration)`, `SetBoundaries(min, max)`, `SnapToTarget()` | A new follow script |

```csharp
// In the host (an ActionRpgHost subclass)
[SerializeField] private GameObject _arrowPrefab;

private void ConfigureArrows()
{
    var spawner = Host.Container.Resolve<IPrefabSpawner>();
    spawner.Register("arrow", _arrowPrefab);
    spawner.ConfigurePool("arrow", initialSize: 16, maxSize: 64);
}

private async UniTaskVoid ShootAsync(Vector3 from, Quaternion aim)
{
    var spawner = Host.Container.Resolve<IPrefabSpawner>();
    var arrow = await spawner.SpawnAsync("arrow", from, aim);
    // ... when it lands:
    spawner.Despawn(arrow);   // back to the pool
}
```

## Gameplay AI helpers

| Need | Use this | Not that |
|---|---|---|
| States with enter/update/exit (AI moods, door open/closed, game flow) | `StateMachine<TStateId>` (enum ids): `RegisterState(state)` with `IState<TStateId>` (`StateId`, `Enter(previous)`, `Update()`, `Exit()`), `Initialize()`, `ChangeState(id)`, `Update()` | `switch (state)` blocks in Update |
| States whose changes take time (menus, loading, cutscenes) | `AsyncStateMachine<TStateId>`: `Register(state)` (derive `AsyncState<TStateId>`), `Allow(from, to...)`, `await ChangeStateAsync(to, payload)`; transitions are queued, `StateChanged` event | Coroutine chains |
| Decision making (patrol, chase, flee) | `BehaviorTree`: `new BehaviorTree(new SelectorNode(new SequenceNode(new CanSeePlayer(), new Chase()), new Patrol()), blackboard)`, `Tick()` each think step; your nodes derive `ConditionNode` (`CheckCondition`) and `ActionNode` (`OnExecute` returns `NodeStatus`); also `InverterNode`, `RepeaterNode`; shared memory in `Blackboard.SetValue/GetValue` | Nested if/else AI |
| Find a path on a grid | `new AStarPathfinder(new GridNavigationGraph(width, height, allowDiagonal: true))`; `graph.SetWalkable(x, y, false)`, `FindPath(new Position2D(0, 0), new Position2D(8, 5))`, `HasPath`; your own graph implements `INavigationGraph` | A hand-written A* or BFS |

Add the module's asmdef (`RPGFramework.StateMachine.Runtime`, `RPGFramework.BehaviorTree.Runtime`,
`RPGFramework.Pathfinding.Runtime`) to the assembly that uses it. Unity's NavMesh is fine for 3D walking; use the
framework's pathfinding for grid and tile logic in the rules.

## UI

| Need | Use this | Not that |
|---|---|---|
| Screens and menus with back navigation | `INavigationService` (`NavigationService(factory, transition, bus, logger)`): `await NavigateToAsync("inventory")`, `GoBackAsync()`, `GoToRootAsync()`, `bool yes = await ShowModalAsync<bool>("confirm-quit")`; `NavigationChangedEvent` on the bus | `SetActive` toggles between panels by hand |
| Create screens from prefabs | `PrefabViewFactory(uiRoot, assets, logger).RegisterAll(viewConfigs)` with `ViewConfig` assets (view id, prefab or asset key, keep instance, layer); screens derive `NavigableView` (modals `ModalView`) | `Instantiate` of UI prefabs |
| Screen transitions | `new CanvasGroupTransition { Duration = 0.2f }`, per pair `service.Transitions.Set("map", "inventory", TransitionStyle.Fade)` | Hand-made fades per screen |
| Clear screens on a scene change | `SceneNavigationHandler` connects navigation to `ISceneDispatcher` | Manual cleanup in every scene |

## Diagnostics

| Need | Use this | Not that |
|---|---|---|
| See systems, events, timers, pools, frame times in the game | `DiagnosticsModule` + `DiagnosticsOverlay` (F3; `ActionRpgHost` has them in development builds); your own panel: an `IDiagnosticsSection` added to `DiagnosticsHub` | `OnGUI` debug text |
| Frame time and memory | `PerformanceMonitor` (`AverageFps`, `WorstFps`, `ManagedMemoryBytes`, `GcCollections`) | `Time.deltaTime` averaging by hand |
| Draw shapes in the Scene view | `DebugDraw.DrawCircleXZ(center, radius, color)`, `DrawBox`, `DrawArrow`, `DrawSphere`, `DrawCross` | Line-by-line `Debug.DrawLine` circles |

## Networking

Co-op, PvP and session providers: recipes 11 and 13. Never open sockets or write a protocol in the game.

## When a line really needs the Unity call

Some calls are right: a console message that needs a context object to click, destroying a placeholder the spawner
never made, a material the view created. End the line with a reason, and GA13 accepts it:

```csharp
Object.Destroy(_ringMaterial); // framework-ok: a material this view made, not a spawned object
```

Editor code (`<Game>.Editor`, `#if UNITY_EDITOR`) and tests are not checked. If the framework's service cannot do what
the game needs, that is a framework gap: record it (`FRAMEWORK-GAPS.md`, GA12), do not rebuild the service.
