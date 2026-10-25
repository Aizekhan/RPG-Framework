# RPGFramework ECS Runtime

A standalone, dependency-light ECS runtime slice being established as the future canonical implementation for the RPG Framework.

## Public surface
- `IComponent`
- `IEntityManager`
- `EntityManager`

## Dependencies
- .NET standard library only.
- No UnityEngine, UnityEditor, MythHunter, game-phase, logger, DI-container, or provider references.

## Migration boundary
This module is the canonical ECS contract and runtime for MythHunter after the EPIC 03.6 consumer migration. MythHunter-facing systems, component caches, factories, archetype code, serializers and DI now depend on `RPGFramework.ECS`. `EcsWorld`, system lifecycle, caches and gameplay policies remain in the MythHunter/Game Layer. The old `MythHunter.Core.ECS.IComponent`, `IEntityManager` and duplicate `EntityManager` have been removed rather than wrapped, so there is one CLR identity and one entity store.

The integer entity ID representation is retained. Archetypes, caches, system lifecycle, serializers and game-specific factories remain outside this assembly and consume the Framework API from the Game Layer.

## Behavior retained for compatibility in this slice
- IDs start at 1 and increase for the manager lifetime.
- Destroying an unknown ID is a no-op.
- Adding a component for an unknown or destroyed ID throws `ArgumentException`; it does not create a phantom entity. This is covered by a focused test.
- `GetComponent` returns `default` when a component is absent; use `HasComponent` / `TryGetComponent` when absence matters.

## Validation status
- Static assembly-boundary/GUID validation: passed in GitHub Actions.
- Headless .NET test project: 8 passed, 0 failed, 0 skipped in GitHub Actions.
- Unity CLI EditMode run on PR head `3917e6a48344d4469fd95a2049b1a2e60d983223` with Unity `6000.0.45f1`: Framework test assembly discovered; 8 ECS tests passed, and the full EditMode run passed 9/9 tests, 0 failed, 0 skipped.
- The successful EditMode test invocation also exercised Unity's project/test assembly import sufficiently to discover and execute the test assembly; this is not represented as a separate clean-room build command.

The standalone Unity ECS test gate passed on the preceding runtime commit. The EPIC 03.6 migration branch must additionally pass full MythHunter compilation, EditMode tests, and a game bootstrap/ECS smoke check before merge.
