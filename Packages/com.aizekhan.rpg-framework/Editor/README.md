# RPG Framework - Editor

Editor-only helpers shared by the framework (`RPGFramework.Editor`, never in player builds).

## What is here

| Folder | Contents |
|---|---|
| `EditorWindows/` | **Framework Manager** (Ctrl+Shift+F, module overview), **Item Database** and **Loot Table Editor** windows |
| `PropertyDrawers/` | `QuestIdDrawer`, `ItemRarityDrawer`, `FactionStandingDrawer`, `ProgressBarDrawer` |
| `Gizmos/` | `FrameworkGizmos`: scene markers (quest, interaction radius, loot spawn, station, dialogue trigger, territory, ranges, paths, zones) |
| `Utilities/` | Menu items (**Tools > RPG Framework**), `FrameworkValidation`, `FrameworkEditorUtility` |

## Designing content

Content is authored as assets, each with its own inspector that lists what is wrong in plain words before you press
Play, and a conversion to the module's runtime data. They live in `Tools/`, one folder per module (Runtime, Editor, Tests):

| Create > RPG Framework > | Module | Turns into |
|---|---|---|
| Item, Item Database | Items | `IItem` |
| Loot Table, Loot Table Database | Loot | `ILootTable` |
| Quest Asset, Quest Database | Quests | `QuestData` |
| Dialogue, Dialogue Database | Dialogue | `DialogueData` / `IDialogue` (the inspector shows the conversation outline) |
| Faction Database | Factions | `FactionRegistry` (the inspector has a relationship grid; works with PvP `FactionHostility`) |
| Skill, Skill Tree | Skills | `SkillData` / `SkillTree` (prerequisite loops are reported; the tree shows by tier) |
| Ability, Ability Database | Abilities | `Ability` / `IAbilityRegistry` |
| Crafting Recipe, Crafting Recipe Database | Crafting | `CraftingRecipeData` (ingredients and results are `ItemAsset`s) |
| Status Effect, Status Effect Database | StatusEffects | `StatusEffectData` (the inspector says what the effect does in one line) |

**Tools > RPG Framework > Create** makes the same assets through a save dialog.

The action RPG's own assets (`Character`, `Game Setup`) are in `ActionRpg/Unity`.
