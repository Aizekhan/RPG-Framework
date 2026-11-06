# Recipe: the framework's tools — measure, debug and check before you build your own

The framework ships tools for the questions every game asks: is this scene too heavy for the device, why did a blow
miss, is the content broken, what is in the project, what do players do. **Before you write a profiler, a debug view, a
data check, a script that lists assets or a telemetry client, find the tool here.** The full list of what the
installed version can do, tools included, is `capabilities.json` at the package root
(`Library/PackageCache/com.aizekhan.rpg-framework@<hash>/capabilities.json`, module `tools` and the others).

## Performance: what a frame draws, frame time, memory

| Question | Tool | How |
|---|---|---|
| How many triangles, draw calls and set-pass calls does this scene have, and which models are the heaviest? | `RenderSection` (Diagnostics.Unity) | In the kit: the diagnostics panel's Render block (`ActionRpgHost` field Diagnostics Key, F3 by default; `ActionRpgHost.Render`). Elsewhere: `new RenderSection(() => camera, budget)` in a `DiagnosticsHub`. `Measure()` → `RenderSample`; `Heaviest` lists models by triangles |
| What may a frame draw on the device the game is for? | `RenderBudget` with `Platform` (`RenderPlatform`) | Pick the device class in the host's Render Budget field, or in code `RenderBudget.For(RenderPlatform.Mobile)`. `Auto` takes the platform the game runs on (`RenderBudget.PlatformOf`). Presets (triangles in view / draw calls / set-pass): `LowEndMobile` 150k / 100 / 60, `Mobile` 300k / 150 / 100, `HighEndMobile` 800k / 300 / 150, `StandaloneVR` 500k / 150 / 80, `WebGL` 500k / 300 / 120, `Handheld` (Switch, Steam Deck) 600k / 500 / 150, `Desktop` 1.5M / 1500 / 250, `HighEndDesktop` (PS5, Xbox Series, RTX 3060+) 5M / 3000 / 500; one model's mesh at most a tenth. `Custom` uses the numbers you type. Values over it are marked OVER |
| Does a scene stay within the budget? (a test, not a guess) | `RenderSection.Measure()` in a PlayMode test | Load the scene, move the camera to each typical view (start, the busiest place, a fight, an open window) and wait until it arrives (the kit's follow camera eases), then assert `Measure().Triangles` and `Heaviest` against the budget. GPU counters (draw calls, set-pass, batches, triangles with shadows) exist only in the Editor and development builds; the CPU count of what is in view works everywhere, also without graphics |
| Frame rate, worst frames, garbage collections, managed memory | `PerformanceMonitor` (`AverageFps`, `WorstFps`, `ManagedMemoryBytes`, `GcCollections`) | The panel's performance section, or read it in a test |
| What runs every frame, which events fire, which timers and pools are alive | `DiagnosticsModule` + `DiagnosticsOverlay` (systems, events, timers, pools sections; `IDiagnosticsSection` for your own) | The kit turns it off in release builds (`Debug.isDebugBuild`); `DiagnosticsOverlay.ToggleKey` sets the key |

A measured number is true only for the commit, scene and device it was measured on: write them next to it.

## Debugging game logic

| Question | Tool |
|---|---|
| Why did a blow miss, why did a monster not attack, what did it decide? | `CombatTimeline` (ActionCombat): phases, contacts with their result or the reason they were ignored, refusals with a reason, every enemy decision; `TimelineEntry`, `For(entity)`. The `Hitbox` gizmo draws the sweep in the Scene view |
| How well do the attacks land, which combos work? (balancing) | `CombatAnalytics`: hit / miss / interrupt rates and average distance per attack, failed approaches, best and worst chains (recipe 18) |
| Draw a radius, a box, an arrow in the Scene view | `DebugDraw` (`DrawCircleXZ`, `DrawBox`, `DrawArrow`, `DrawSphere`, `DrawCross`) |
| Is a quest or a dialogue graph broken (dead ends, unreachable stages, missing links)? | `QuestGraph.Check`, `DialogueGraph.Check` |
| Is an enemy or an attack asset set up wrong? | `EnemyBehaviourAsset.Problems()`, `AttackAsset.Problems()` (also logged as inspector warnings) |

## Checking the project

| Question | Tool |
|---|---|
| Does the game still follow the architecture (GA01–GA15)? | `<Game>ArchitectureTests` (derives from `GameArchitectureTests`, created by the New Game template); menu RPG Framework > Game Kit > Check Architecture |
| Are quests, dialogues, factions, recipes, items and loot valid? | `FrameworkValidation.ValidateFromCommandLine` (exit 0 clean, 1 errors, 2 the check failed; recipe 19); menu RPG Framework > Tools > Utilities > Validate Framework Data |
| What is in the project (assets with GUIDs and references, scenes, prefabs, scripts, framework modules used)? | `ProjectIndexCommand.ExportFromCommandLine` (recipe 19); menu RPG Framework > Tools > Export Project Index. Read the index instead of walking `Assets/` by hand |
| Content written in another tool (quests, dialogues) | `ContentImport`, JSON `rpgframework.content/1` (recipe 20); menu RPG Framework > Tools > Import Content (JSON) |
| Every batch-mode entry point and its exit code | Recipe 19 |

## Players and playtests

| Question | Tool |
|---|---|
| What do players do (deaths, quests, where they get stuck, survey answers, A/B variants)? | `Telemetry`: `TelemetryModule(endpoint, key)` registers `ITelemetry`; `Track`, `SamplePosition`, `AnswerSurvey`. Opt-in consent (`Consent`), batches off the game thread, retries |
| Combat numbers to the same collector | `CombatTelemetry` (Integration.ActionCombatTelemetry): `combat_hit`, `combat_miss`, ..., `combat_summary` |

## Tests without a scene, a network or speakers

| Need | Tool |
|---|---|
| Two players on one machine, no sockets | `LoopbackNetwork` / `LoopbackTransport` (recipe 11) |
| Sound in a test or on a server | `RecordingAudioOutput` given to `AudioModule` |
| Test helpers for characters and UI | the `TestSupport` assemblies of `Characters`, `UI`, `UINavigation`, `GameKit` |

## Designer windows (menu RPG Framework > Tools, and Game Kit)

Framework Manager, Item Database, Item Creation Wizard, Loot Table Editor and Wizard, Quest Creation Wizard, Game Kit
> New Game or Feature, Create > Quest / Dialogue / Faction Database / Skill / Skill Tree / Ability / Crafting Recipe /
Status Effect (under Tools). Visual graph editors for dialogue, quests and skill trees do not exist yet (KI-18 of the framework).

## When a tool is missing

A tool every game would need (a measurement, a check, a debug view) is a framework gap like any other: record it in
`FRAMEWORK-GAPS.md` (see `CLAUDE.md`, "When the framework lacks something") instead of building a private one.
