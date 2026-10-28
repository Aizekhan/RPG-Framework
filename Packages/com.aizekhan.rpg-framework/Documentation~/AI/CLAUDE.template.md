<!-- rpg-framework:begin (installed by RPG Framework > Game Kit > Install AI Guidance; reinstalling replaces this block) -->
# Building this game on RPG Framework

This game is built on the RPG Framework package (`com.aizekhan.rpg-framework`). Follow its architecture.
Automated tests enforce the architecture, so a change that breaks it fails.

## Read before changing code
- `Docs/RPGFramework/architecture.md`: the layers and the rules GA01–GA14 (test failures name the rule).
- `Docs/RPGFramework/recipes/16-game-needs-to-modules.md`: which module a game need goes to (a chest → loot table,
  a shop → Trading, a perk → Abilities, poison → StatusEffects). GA14 fails on rules that hand-roll them.
- `Docs/RPGFramework/recipes/`: step-by-step recipes. Find the one that matches the task and follow it.
- The framework's own README (`Packages/com.aizekhan.rpg-framework/README.md`, or the package in `Library/PackageCache`)
  lists the modules. **Use a framework module before writing your own**: combat, stats, progression, quests,
  inventory, items, loot, dialogue, crafting, factions, persistence, UI navigation, spawning, pooling and timers exist.

## Genre kits
- Real-time action RPG: `ActionRpg` (the New Game template; recipes 01-13).
- Turn-based party RPG: `TurnBased` (turn order, actions, abilities, status effects, enemy AI, rewards, levels, saves;
  recipe 15). It has no battle screen: build the views on `ITurnBasedWorld` / `Battle`, never your own battle rules.

## Core services — always use these
Read `Docs/RPGFramework/recipes/14-core-services.md` before writing gameplay plumbing. The framework's services are in
the container (`Host.Container.Resolve<T>()` in the host, constructor parameters in rules); never write your own:
- spawn/despawn objects: `IPrefabSpawner` (`ConfigurePool`, `SpawnAsync`, `Despawn`), not `Instantiate`/`Destroy`;
  pool plain objects: `ObjectPool<T>` + `IPoolManager`, not a `Stack<T>`
- load assets: `IAssetManager.LoadAsync<T>` (dispose the handle), not `Resources.Load`; scenes: `ISceneDispatcher`,
  not `SceneManager.LoadScene`
- delays and repeats: `ITimerService` (`Schedule`, `Repeat`, `DelayAsync`), not `Invoke`/coroutines; per-frame
  logic: systems (`SystemBase`, `AddSystem<T>()`); logs: `ILogger`, not `Debug.Log`; messages: the event bus
- entities: `IEntityManager`, `ArchetypeCatalog`, `GetEntitiesWith<T1, T2>(list)`; AI: `StateMachine<T>`,
  `AsyncStateMachine<T>`, `BehaviorTree`, `AStarPathfinder`; UI: `INavigationService`; saves: recipe 12
GA13 fails on `Resources.Load`, `SceneManager.LoadScene`, `Debug.Log`, `Invoke`, `WaitForSeconds`, `Instantiate`,
`Destroy` and hand-made pools in game code. A line that truly needs the Unity call ends with
`// framework-ok: <reason>`.

## The shape of the game
```
Assets/<Game>/
  Scripts/Core/    <Game>.Core asmdef, noEngineReferences: rules, state, events, modules, systems (plain C#)
  Content/         Game Setup, Characters, items, loot tables, quests (ScriptableObjects designers edit)
  Scripts/         <Game> asmdef: host (ActionRpgHost or GameHostBehaviour), views, input, HUD (MonoBehaviours)
  Editor/          <Game>.Editor asmdef: menus, builders, inspectors
  Tests/           <Game>.Tests asmdef: EditMode tests + <Game>ArchitectureTests
  PlayTests/       PlayMode tests that play a scene
  Config/ Prefabs/ Scenes/ Art/
```

## Non-negotiable rules
1. **Rules in `<Game>.Core`, no UnityEngine.** A rule decides what happens; a view only shows it and passes
   input in. Views call the rules' API; they never change state themselves.
2. **Modules compose the game.** Services are registered in a `GameFeatureModule` (`Container.RegisterFactory`,
   `AddSystem<T>()`) and added in `<Game>Composition.Configure`. Dependencies come in through constructors.
   No singletons, no static state, no `FindObjectOfType`, no `GameObject.Find`.
3. **React through the event bus.** Something happened → publish an `IEvent` (`readonly struct`, past-tense name,
   e.g. `ChestOpenedEvent`). Another feature cares → subscribe to it. Features do not call each other directly.
4. **Designers tune, code reads.** Numbers, prefabs and content live in ScriptableObjects or `[SerializeField]`
   fields, not in constants. Characters, enemies, items, loot and quests go in the Game Setup and Character assets
   (recipe 10); code only adds rules the assets cannot express.
5. **Every change comes with tests.** Rules: EditMode tests that boot `GameHost` with the composition.
   Scenes: a PlayMode test. Never delete or weaken a test to make it pass.
6. **Every new file and folder under `Assets/` gets its `.meta`**: let Unity import (open the Editor or run it in
   batch mode); never write a `.meta` by hand with a made-up GUID.

## When the framework lacks something (never cut the corner)
0. **Before adding state or a system to `<Game>.Core`** (a list of what the player owns, a copy of the money balance,
   a timer, a timed state, a shop, a pause flag, a co-op message), find its row in recipe 16. Writing it yourself when a module
   has it is the corner this section forbids, whether or not a test catches it: GA14 only knows common shapes.
1. Search first: the package README's module list, `Docs/RPGFramework/recipes/07-use-framework-modules.md`, and the
   package source (`Library/PackageCache/com.aizekhan.rpg-framework*`). Most RPG needs are already there.
2. If it really is missing: **do not** rebuild the module in the game, **do not** shrink the feature to fit, and
   **do not** fake it with a hack. Tell the developer what is missing and why the feature needs it.
3. Record it in `FRAMEWORK-GAPS.md` (project root): a section `## <id>` with what the game needs, what the framework
   lacks, the API you would want, and where the stand-in lives.
4. Write the smallest stand-in behind an interface in `<Game>.Core`, marked `// framework-gap: <id>` on the line
   above it, with tests. GA11 fails on rebuilt framework types; GA12 fails on a marker with no section.
5. If the gap is big (a whole system), stop and ask whether to add it to the framework first.

## After the framework is updated (the tag in `Packages/manifest.json` changed)
1. Read the new version's `CHANGELOG.md` in the package, then reinstall the AI guidance
   (`-executeMethod RPGFramework.GameKit.Editor.AIGuidance.InstallFromCommandLine`).
2. **Go through every `## Open` section of `FRAMEWORK-GAPS.md`** against the changelog and the package source. A gap
   the new version covers is not done until its stand-in is gone: replace the code marked `// framework-gap: <id>`
   with the framework's API, delete the stand-in and its marker, keep or move its tests, and move the section to
   `## Resolved` with the framework version. Leaving a stand-in next to the module that replaces it is duplication.
3. Run all tests; record what moved in `DEVLOG.md`.

## The game's own documents (project root)
- `GAME.md`: what the game is: genre, core loop, features in priority order, what it will not have. Read it before
  planning; update it when the developer decides something.
- `FRAMEWORK-GAPS.md`: the gaps above. `DEVLOG.md`: one line per finished feature (date, what, commit).

## Workflow
1. Generate structure instead of writing it: **RPG Framework > Game Kit** (New Game, New Feature).
   Batch mode: `Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine -game <Game> -feature <Feature>`.
2. Implement the rules in `<Game>.Core` with tests, then the view.
3. Run the tests: `unity test . --mode EditMode` and `--mode PlayMode` (or Unity Test Runner).
   `<Game>ArchitectureTests` must stay green.
4. Commit with conventional messages (`feat:`, `fix:`, `test:`, `docs:`).
<!-- rpg-framework:end -->
