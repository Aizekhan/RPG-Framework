# Reference Game - RPG Framework Demo

## Overview

Reference Game demonstrates how to create a complete RPG using the RPG Framework. This is a **minimal viable game** that showcases Framework's core features without any game-specific dependencies.

## Purpose

1. **Validate Framework Independence** - Prove Framework can create games without MythHunter dependencies
2. **Developer Template** - Provide starting point for new RPG projects
3. **Framework Showcase** - Demonstrate all Framework systems working together
4. **Best Practices** - Show proper Framework integration patterns

## Current Implementation Status

### ✅ Phase 1: Foundation (COMPLETE)

**Game-Specific Classes:**
- `PlayerStats` - Concrete stats implementation extending Framework's `IStats`
- `ReferenceGamePlayer` - Player character extending Framework's `CharacterBase`
- `SimpleMovement` - Basic WASD movement controller
- `GameManager` - Game initialization and testing

**Framework Integration:**
- ✅ Stats System - Health, Mana, Attributes (STR/AGI/INT/VIT)
- ✅ Characters System - Player character with Framework base class
- ✅ Combat System - Damage/Heal functionality
- ✅ Debug UI - OnGUI stats display

**Files Created:**
```
ReferenceGame/
├── Scripts/
│   ├── Stats/
│   │   └── PlayerStats.cs          (76 lines)
│   ├── Characters/
│   │   ├── ReferenceGamePlayer.cs  (74 lines)
│   │   └── SimpleMovement.cs       (62 lines)
│   └── GameManager.cs               (87 lines)
├── Scenes/
│   └── GoblinValley.unity
└── ReferenceGame.asmdef
```

### 🚧 Phase 2: Core Systems (NEXT)

- [ ] Create Player GameObject prefab
- [ ] Test in PlayMode
- [ ] Add ground plane for movement
- [ ] Inventory System integration
- [ ] Progression System integration (XP/Leveling)

### 📋 Phase 3: Extended Features (FUTURE)

- [ ] Enemy AI (simple goblin)
- [ ] Quest System integration
- [ ] Loot System integration
- [ ] Equipment System
- [ ] UI Panels (using Framework UI)

## How to Use

### Setup in Unity Editor:

1. **Open Scene:**
   - Navigate to `Assets/ReferenceGame/Scenes/GoblinValley.unity`
   - Double-click to open

2. **Create Player GameObject:**
   ```
   - Create Empty GameObject named "Player"
   - Add ReferenceGamePlayer component
   - Add CharacterController component
   - Add SimpleMovement component
   - Position at (0, 1, 0)
   ```

3. **Create GameManager:**
   ```
   - Create Empty GameObject named "GameManager"
   - Add GameManager component
   - Assign Player reference in Inspector
   ```

4. **Create Ground:**
   ```
   - Create 3D Object > Plane
   - Position at (0, 0, 0)
   - Scale to (10, 1, 10)
   ```

5. **Press Play!**

### Controls:

- **WASD** - Move player
- **Space** - Test damage (15 HP)
- **H** - Heal (20 HP)

### Expected Behavior:

- Player spawns with full health/mana
- Stats displayed in top-left corner (OnGUI)
- Controls shown in top-right corner
- Console shows initialization logs
- Framework integration verified ✓

## Architecture Patterns

### Marker Interface Pattern

**Framework provides:**
```csharp
public interface IStats { } // Marker interface
```

**Game implements:**
```csharp
public class PlayerStats : IStats
{
    public float CurrentHealth { get; private set; }
    public float MaxHealth { get; private set; }
    // ... concrete properties
}
```

### Inheritance Pattern

**Framework provides:**
```csharp
public abstract class CharacterBase : MonoBehaviour, ICharacter
{
    protected IStats stats;
    // ... base functionality
}
```

**Game extends:**
```csharp
public class ReferenceGamePlayer : CharacterBase
{
    private PlayerStats playerStats;
    public override string Name => "Hero";
    // ... game-specific logic
}
```

## Compilation Status

**✅ 0 Compilation Errors**
**✅ Clean Framework Integration**
**✅ No Game-to-Framework Coupling**

## Next Steps

1. Test in PlayMode
2. Add more Framework systems (Inventory, Progression)
3. Create simple enemy for combat testing
4. Integrate Quest system
5. Add UI panels from Framework

## Code Statistics

- **Total Lines:** 299 lines of game code
- **Framework Dependencies:** 2 assemblies (Characters, Stats)
- **Compilation Time:** ~3 seconds
- **Integration Complexity:** Very Low ✅

## Success Criteria

- [x] Framework compiles with game code
- [x] No circular dependencies
- [x] Game extends Framework without modifying it
- [x] Clean separation of concerns
- [ ] Playable in Unity Editor
- [ ] All Framework systems demonstrated

---

**Status:** Foundation Complete ✅
**Next:** Runtime Testing in Unity Editor
