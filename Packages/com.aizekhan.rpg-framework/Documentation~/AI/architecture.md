# Game architecture on RPG Framework

<!-- Installed by RPG Framework > Game Kit > Install AI Guidance; reinstalling overwrites this file. -->

## Layers

```
┌──────────────────────────────────────────────────────────────────────┐
│ <Game> (Unity)   Host · Views · Input · HUD · Presenter              │  shows state, passes input in
├──────────────────────────────────────────────────────────────────────┤
│ <Game>.Core      Modules · Rules · Systems · Events · Content ids    │  decides what happens
├──────────────────────────────────────────────────────────────────────┤
│ RPG Framework    GameHost · DI · EventBus · Systems · gameplay modules│  building blocks
└──────────────────────────────────────────────────────────────────────┘
```

- **GameHost** builds the game from modules: `FrameworkCoreModule` (event bus, entities, systems, timers) and the
  modules listed in `<Game>Composition.Configure`. A module registers services in the container and declares the
  modules it depends on; the host starts them in dependency order and fails loudly when one is missing.
- **Rules** are plain C# classes resolved from the container. They own the state and publish events when
  something happens.
- **Systems** (`IUpdateSystem`, added with `AddSystem<T>()`) are rules that run every frame (waves, regeneration,
  cooldowns). `GameHostBehaviour` ticks them from Unity.
- **Views** are MonoBehaviours on prefabs. A presenter subscribes to "entity spawned/despawned" events, spawns the
  prefab for the entity's kind through `IPrefabSpawner`, links it in `IEntityViewRegistry` and hands the view the
  rules' API. Views read state every frame and call the API on input.
- **Content and tuning** live in assets (ScriptableObjects, `PrefabCatalog`) edited in the inspector.

The reference game `Assets/ReferenceGame` (Goblin Valley) follows all of this. Read it when unsure.

## Rules

Each rule is a test in `<Game>ArchitectureTests`. A failure prints the rule id, the place and the fix.
A single source line may opt out of a source rule (GA05, GA08) with a reason:
`// architecture: allow GA05 (third-party plugin needs it)`. Use it rarely.

### GA01 Game code lives in assembly definitions
Scripts without an `.asmdef` compile into `Assembly-CSharp`, where no boundary can be checked.
Give every folder of scripts an `.asmdef`.

### GA02 The game has a rules assembly
Rules live in an assembly named `<Game>.Core`.

### GA03 Rules do not use UnityEngine
`<Game>.Core` has `"noEngineReferences": true`. Rules that do not touch the engine run in fast EditMode tests,
on a server, in a simulation, and cannot depend on what is in a scene. Need a position, time or random number?
Pass it in as a parameter or a framework service (`ITimerService`, the seed in options).

### GA04 Rules do not depend on views, editor code or tests
The dependency points one way: views → rules. Rules that need to tell a view something publish an event.

### GA05 No scene searches or messages by name
`FindObjectOfType`, `FindFirstObjectByType`, `GameObject.Find`, `SendMessage` hide dependencies and break when
the scene changes. Services come from the container, views from `IEntityViewRegistry`, scene objects from
`[SerializeField]` fields on the host.

### GA06 No singletons
A static `Instance` is global mutable state: tests interfere with each other and order of initialization becomes
luck. Register the service in a module and inject it.

### GA07 Events are immutable structs
`public readonly struct SomethingHappenedEvent : IEvent` with get-only properties set in the constructor. Handlers
cannot change an event under the next handler, and publishing does not allocate.

### GA08 UnityEditor only in editor code
`UnityEditor` does not exist in a player build. Editor tools go to `<Game>.Editor` (an `.asmdef` with
`"includePlatforms": ["Editor"]`) or inside `#if UNITY_EDITOR`.

### GA09 Unity objects live in files of their own name
Unity can attach a MonoBehaviour or create a ScriptableObject only when the class `Foo` lives in `Foo.cs`.

### GA10 Every rules and runtime assembly has tests
A test assembly references each of them. Rules are tested by booting `GameHost` with the game's composition;
scenes by a PlayMode test.
