# Architecture of this game

> How this game is built on RPG Framework: the map a new session reads before changing structure. The framework's
> own rules are in `Docs/RPGFramework/architecture.md`; this file describes only this game. Update it when a feature
> adds an assembly, a module, a system, an event, a content asset type or a saved part (`/handoff` checks it).

## Assemblies
| Assembly | What is in it | Depends on |
|---|---|---|
| `<Game>.Core` | rules, no UnityEngine | RPG Framework runtime modules |
| `<Game>` | views, input, scene glue | `<Game>.Core`, framework Unity bridges |
| `<Game>.Tests` | EditMode and PlayMode tests | both |

## Composition
- Genre kit: <ActionRpg / TurnBased / none>. Composition root: `<Game>Composition` (modules in order).
- Framework modules used: <module: what for>.

## Features
| Feature | Rules (Core) | View | Content assets | Events | Tests |
|---|---|---|---|---|---|

## Systems and phases
- <system: what it does each frame, in which phase>

## Saves
- <SaveParticipant: what it keeps>

## Networking
- <single player / co-op host-client / turns: protocol, what is synced>

## Stand-ins for framework gaps
- <gap id: stand-in type, where; see FRAMEWORK-GAPS.md>

## Decisions
| Date | Decision | Why |
|---|---|---|
