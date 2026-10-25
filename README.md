# RPG Framework

**Universal RPG Framework for Unity 6**

A comprehensive, modular, and reusable RPG framework designed to work with any RPG game.

## Features

### Core Systems
- **Dependency Injection** - LazyMonoBehaviour with automatic injection
- **Event System** - Type-safe messaging system
- **Object Pooling** - Efficient object reuse
- **State Machine** - Generic hierarchical state machines

### Gameplay Systems
- **Abilities** - Flexible ability system with cooldowns and resources
- **Characters** - Stats, attributes, levels, experience
- **Combat** - Damage calculation, effects, targeting
- **Dialogue** - Branching conversations with choices
- **Inventory** - Items, equipment, currencies
- **Quests** - Quest tracking with objectives
- **Save System** - Serialization and persistence

### AI Systems
- **Behavior Trees** - Modular AI behavior composition
- **Pathfinding** - NavMesh integration
- **Sensors** - Perception and awareness

### Infrastructure
- **UI System** - Presenter/View pattern for UI panels
- **Scene Management** - Scene loading and transitions
- **Resource Management** - Asset loading and caching
- **Debug Tools** - Development utilities

## Project Structure

```
Assets/
  _Framework/           # Framework modules (35+ assemblies)
    Abilities/
    AI/
    Characters/
    Combat/
    DI/
    Dialogue/
    Events/
    Inventory/
    Quests/
    SaveSystem/
    StateMachine/
    UI/
    ... and more
  _Tests/              # EditMode tests
    Editor/
```

## Requirements

- **Unity:** 6000.0.45f1 or later
- **Packages:**
  - Input System 1.11.2
  - TextMeshPro 3.2.0-pre.10
  - UGUI 3.0.0

## Installation

1. Clone this repository
2. Open in Unity 6000.0.45f1 or later
3. Framework will compile automatically

## Development Status

🚧 **Active Development** - EPIC 25: Infrastructure Enhancement

Current Phase: Phase 1 Complete (DI Enhancement)

See [EPIC-25-INFRASTRUCTURE-PLAN.md](EPIC-25-INFRASTRUCTURE-PLAN.md) for roadmap.

## License

MIT License - See LICENSE file for details

## Authors

Aizekhan - [GitHub](https://github.com/Aizekhan)

---

🎮 Built with ❤️ for RPG developers
