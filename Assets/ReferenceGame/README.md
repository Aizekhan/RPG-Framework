# Reference Game: Goblin Valley

A small game built the way a user of the framework builds one: the framework's action RPG assembled from assets in
the inspector, real model prefabs, and one rule of its own in code.

## Play

Open `Scenes/GoblinValley.unity` and press Play. Waves of goblins and goblin brutes come at the hero, one wave
bigger than the last.

- **Move**: WASD, arrows or the left stick
- **Attack**: Space, left mouse button or the gamepad south button
- **Character sheet and backpack**: I, Tab or gamepad Select
- **Respawn**: R or Start

Progress (level, backpack, quests) is saved and comes back next Play. To start fresh, right-click the
`GoblinValleyHost` component > **Delete Saved Progress**.

## What is an asset, what is code

| Where | What | Edit it to |
|---|---|---|
| `Content/GoblinValleySetup` (**Game Setup**) | player, enemies, waves, items, starting quests, seed, camera offset | change the game without code |
| `Content/Characters/*` (**Character**) | Hero, Goblin, Goblin Brute: stats, growth, speed, reach, experience, loot table, model prefab | tune or add characters |
| `Content/Items`, `Content/Loot`, `Content/Quests` | Goblin Ear, Rusty Dagger, Brute Tusk, Valley Medal; loot per goblin kind; *Clear the Valley*, *Brute Force* | add content |
| `Prefabs/Hero`, `Goblin`, `GoblinBrute` | models with a strike lunge and a health bar; controls and AI are added in play mode | swap in real art |
| `Scripts/Core/KillStreak.cs` | the game's own rule: kills in a row without being hurt give bonus loot | rules the inspector cannot express |
| `Scripts/GoblinValleyHost.cs` | an `ActionRpgHost` that adds the rule's module; streak numbers in the inspector | |

Everything else (combat, experience and level-ups, loot, quests and their rewards, waves, HUD, saving) is the
framework's `ActionRpg` module.

## How the rule plugs in

`GoblinValleyHost.ConfigureGameModules` adds `GoblinValleyModule`, which registers `KillStreak`. The rule only
listens to events and acts through `ActorWorld`:

| Event | Rule |
|---|---|
| `ActorKilledEvent` (by the player) | the streak grows; every N kills: `ActorWorld.GiveItem`, `Announce`, `KillStreakRewardedEvent` |
| `ActorDamagedEvent` (the hero) | the streak resets |

`GoblinValleyComposition.ConfigureWithActionRpg` builds the same game for tests, without the scene.

## Rebuilding the art and content

**RPG Framework > Reference Game > Rebuild Goblin Valley** (`Editor/GoblinValleyBuilder`) regenerates the placeholder
prefabs and the scene layout. Materials and content assets are only created when missing, so inspector tuning
survives. Batch mode:
`Unity -batchmode -quit -projectPath . -executeMethod ReferenceGame.Editor.GoblinValleyBuilder.Rebuild`

## Tests

- `Tests/GoblinValleyTests`: the setup has its characters, prefabs and quests; the game boots from the setup asset,
  a wave comes and dies with experience and loot; the kill streak gives its bonus, resets when the hero is hurt and
  can be turned off.
- `Tests/GoblinValleySceneTests`: one host, wired to the setup and a camera, saving on, no missing scripts.
- `Tests/ReferenceGameArchitectureTests`: the game architecture rules GA01-GA10.
- `PlayTests/GoblinValleyPlayTests` (PlayMode): plays the scene: the model prefabs appear with controls and AI,
  goblins chase, the hero's strike lands, slain goblins leave and the streak counts.
