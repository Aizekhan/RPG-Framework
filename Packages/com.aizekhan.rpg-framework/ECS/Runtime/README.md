# RPGFramework ECS Runtime

The framework's entity store: entities, typed component pools, queries and archetypes, without UnityEngine.

## Public surface
- `IComponent` (components are structs), `IEntityManager`, `EntityManager` (typed pools without boxing, `GetComponentRef`,
  multi-component queries into a reused list, lifecycle events)
- Archetypes: `EntityArchetype`, `ArchetypeBuilder`, `ArchetypeCatalog`, `ArchetypeTag`

## Dependencies
- .NET standard library only.
- No UnityEngine, UnityEditor, MythHunter, game-phase, logger, DI-container, or provider references.

## Where the rest lives
`World` / `IWorld` and systems are in `RPGFramework.Systems`; the entity id is an `int`.

## Behaviour
- IDs start at 1 and increase for the manager lifetime.
- Destroying an unknown ID is a no-op.
- Adding a component for an unknown or destroyed ID throws `ArgumentException`; it does not create a phantom entity. This is covered by a focused test.
- `GetComponent` returns `default` when a component is absent; use `HasComponent` / `TryGetComponent` when absence matters.

Tests: `ECS/Tests` (including `EcsPerformanceTests`).
