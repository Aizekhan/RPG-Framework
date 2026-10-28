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
- **Core services** are shared by the whole game and resolved from the container, never rebuilt in it: logger,
  event bus, timers, entities and systems (`FrameworkCoreModule`); object pools, assets, the prefab spawner and scene
  loading (`UnityServicesModule`, which `ActionRpgHost` adds). Recipe 14 lists every need and its service; GA13
  checks it.
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
A single source line may opt out of a source rule (GA05, GA08, GA11, GA13; not GA14) with a reason:
`// architecture: allow GA05 (third-party plugin needs it)`. GA13 also accepts `// framework-ok: <reason>` at the end
of the line. Use it rarely.

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

### GA11 The game does not redo the framework
Game code does not declare a type with the name of a framework type (`Quest`, `Item`, `Ability`, ...) and does not
write into the `RPGFramework` namespace. Use the framework's type. If it lacks something the game needs, that is a
**framework gap** (GA12): record it, do not rebuild the module inside the game. A different, game-specific thing gets
a name that says what it is (`BountyBoard`, not `Quest`).

### GA12 Framework gaps are recorded
When the game needs something the framework does not have, the work does not stop and the feature is not quietly
cut. The game gets a stand-in, as small as possible, behind its own interface, with a marker on the line above it:
`// framework-gap: <id>`. `FRAMEWORK-GAPS.md` in the project root has a section `## <id>` for every marker: what the
game needed, what the framework lacks, the API the framework should offer, and where the stand-in lives. A marker
without a section fails this rule. The file is the framework's backlog with proof; when the framework gains the
feature, the stand-in is deleted and the section moved to "Resolved".

### GA13 The game uses the framework's core services
Game code (rules and runtime assemblies; editor builders and tests are exempt, so is code inside `#if UNITY_EDITOR`)
does not do by hand what a framework service already does. Recipe 14 (`recipes/14-core-services.md`) maps every
need to its service. The rule catches the usual re-inventions:

| Found in game code | Use instead |
|---|---|
| `Resources.Load*` | `IAssetManager.LoadAsync<T>(key)`, dispose the handle |
| `SceneManager.LoadScene` / `LoadSceneAsync` / `UnloadSceneAsync` | `ISceneDispatcher` |
| `Debug.Log*` | the game's `ILogger` |
| `Invoke("...")` / `InvokeRepeating`, `new WaitForSeconds` | `ITimerService` (`Schedule`, `Repeat`, `DelayAsync`) |
| `Instantiate(...)` | `IPrefabSpawner.SpawnAsync` (+ `ConfigurePool`), or `IAssetManager.InstantiateAsync` |
| `Destroy(...)` of an object (removing a `GetComponent<...>()` component is fine) | `IPrefabSpawner.Despawn` |
| `Stack<GameObject>`, `Queue<GameObject>`, a collection named `*pool*` | `ConfigurePool` / `ObjectPool<T>` + `IPoolManager` |

Text in strings and comments does not count. When a line really needs the Unity call (a context object for the
console, an object the spawner never made, a material the view created), end it with `// framework-ok: <reason>`;
the reason is required.

### GA14 The game's rules use the framework's gameplay modules
GA11 catches a framework type rebuilt under its own name; GA14 catches the same work done without a type. Rules code
(`<Game>.Core`) does not hand-roll what a gameplay module does. Recipe 16 (`recipes/16-game-needs-to-modules.md`)
maps each game need to its module. The rule looks for:

| Found in rules code | Use instead |
|---|---|
| a random amount of currency (`_random.Next(GoldMin, GoldMax)`) | a loot table entry for the currency (its quantity range) |
| a weighted pick (`roll -= entry.Weight`, `Weighted*.Pick(...)`) in a file that rolls dice | `ILootTable` + `ILootGenerator` |
| currency taken by hand (`profile.Gold -= price`) | `ICurrencyWallet.TrySpend`; a shop: `ITradingManager.Trade` (`TradingModule`) |
| a cooldown counted down or kept in a `Dictionary<..., float>` named `*cooldown*` | an ability: `ActorAbilities.Use` / `IAbilityExecutor`, the game's own action in `SetEffect` |
| an effect timer (`_poisonLeft -= dt`) | a status effect: `ActorEffects.Apply` / `IStatusEffectManager` |
| any other countdown (`_spawnIn -= dt;`) | `ITimerService.Schedule` / `Repeat` |

These are heuristics, so `// architecture: allow GA14` does **not** work. The only way past is a recorded gap: a
`// framework-gap: <id>` marker on the line, on the line above it, or above the type it is in (above its doc comment and attributes too), with its section in
`FRAMEWORK-GAPS.md` (GA12). Either the module does the job, or the framework learns what it lacks.
