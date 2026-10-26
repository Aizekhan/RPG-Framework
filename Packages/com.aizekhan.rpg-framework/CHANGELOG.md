# Changelog

## [Unreleased]

- `Presentation`: `EntityView` and `EntityViewRegistry` link entities to the GameObjects that show them (pooled views can be rebound)

## [0.1.0] - 2026-10-26

First packaged release. The framework moved from `Assets/_Framework` into the embedded package
`com.aizekhan.rpg-framework`; asset GUIDs are unchanged.

- Composition: `GameHost`, `GameHostBehaviour`, ready-made gameplay modules (`Bootstrap.Gameplay`)
- Event bus integration across Combat, Progression, Quests, Inventory, Guilds and Abilities
- Asset management with Resources and optional Addressables providers; `PrefabSpawner` with pooling
- UI navigation (screens, modals, transitions); RPG panels built on it
- Persistence with checksummed save slots and metadata sidecars
