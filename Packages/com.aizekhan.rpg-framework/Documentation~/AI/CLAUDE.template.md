<!-- rpg-framework:begin (installed by RPG Framework > Game Kit > Install AI Guidance; reinstalling replaces this block) -->
# Building this game on RPG Framework

This game is built on the RPG Framework package (`com.aizekhan.rpg-framework`). Follow its architecture.
Automated tests enforce the architecture, so a change that breaks it fails.

## Read before changing code
- `Docs/RPGFramework/architecture.md`: the layers and the rules GA01–GA10 (test failures name the rule).
- `Docs/RPGFramework/recipes/`: step-by-step recipes. Find the one that matches the task and follow it.
- The framework's own README (`Packages/com.aizekhan.rpg-framework/README.md`, or the package in `Library/PackageCache`)
  lists the modules. **Use a framework module before writing your own**: combat, stats, progression, quests,
  inventory, items, loot, dialogue, crafting, factions, persistence, UI navigation, spawning, pooling and timers exist.

## The shape of the game
```
Assets/<Game>/
  Scripts/Core/    <Game>.Core asmdef, noEngineReferences: rules, state, events, modules, systems (plain C#)
  Scripts/         <Game> asmdef: host (GameHostBehaviour), views, input, HUD (MonoBehaviours)
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
   fields, not in constants.
5. **Every change comes with tests.** Rules: EditMode tests that boot `GameHost` with the composition.
   Scenes: a PlayMode test. Never delete or weaken a test to make it pass.
6. **Every new file and folder under `Assets/` gets its `.meta`**: let Unity import (open the Editor or run it in
   batch mode); never write a `.meta` by hand with a made-up GUID.

## Workflow
1. Generate structure instead of writing it: **RPG Framework > Game Kit** (New Game, New Feature).
   Batch mode: `Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine -game <Game> -feature <Feature>`.
2. Implement the rules in `<Game>.Core` with tests, then the view.
3. Run the tests: `unity test . --mode EditMode` and `--mode PlayMode` (or Unity Test Runner).
   `<Game>ArchitectureTests` must stay green.
4. Commit with conventional messages (`feat:`, `fix:`, `test:`, `docs:`).
<!-- rpg-framework:end -->
