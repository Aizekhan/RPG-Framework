# EPIC 25: Infrastructure Extraction - Complete Plan

## Goal
Extract critical infrastructure systems from MythHunter GameLayer to RPG Framework, making Framework 100% self-contained with production-ready utilities.

## Current State (2026-10-25)

**Framework Status:**
- **35/35 modules (100% COMPLETE)** ✅
- UI System extracted ✅
- Inspector Tooling (3 tools) ✅

**Existing Infrastructure in Framework:**
- ✅ DI (Dependency Injection) - `RPGFramework.DI.Runtime`
- ✅ ECS - `RPGFramework.ECS.Runtime`
- ✅ Pooling - `RPGFramework.Pooling.Runtime` (basic)
- ✅ Serialization - `RPGFramework.Serialization.Runtime`
- ✅ Persistence - `RPGFramework.Persistence.Runtime`
- ✅ Networking - `RPGFramework.Networking.Runtime`
- ✅ Logging - `RPGFramework.Logging.Runtime`
- ✅ StateMachine - `RPGFramework.StateMachine.Runtime`

**Missing Infrastructure (in GameLayer):**
- ❌ SceneManagement (scene loading with data passing)
- ❌ Systems (System Registry, Parallel execution, Priorities)
- ❌ Resources (Resource loading, Preloading)
- ❌ Debug (Debug Dashboard, Performance Monitor, Profiler)
- ❌ Time (Timer System)

**Infrastructure to Enhance:**
- ⚠️ DI - Add LazyMonoBehaviour, LazyDependencyInjector
- ⚠️ Pooling - Add advanced features (scene tracking, lifetime tracking)

---

## EPIC 25 Deliverables

### Phase 1: DI Enhancement ⚠️ IN PROGRESS

**Goal:** Add Unity MonoBehaviour integration for DI

**Files to Create:**
1. `Assets/_Framework/DI/Runtime/LazyMonoBehaviour.cs` ⚠️ STARTED
2. `Assets/_Framework/DI/Runtime/LazyDependencyInjector.cs`
3. `Assets/_Framework/DI/Runtime/IDependencyInjector.cs`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Core/MonoBehaviours/LazyMonoBehaviour.cs`
- `Assets/_MythHunter/Code/Core/MonoBehaviours/LazyDependencyInjector.cs`
- `Assets/_MythHunter/Code/Core/MonoBehaviours/IDependencyInjector.cs`

**Tests:**
- Create `Assets/_Framework/DI/Tests/LazyMonoBehaviourTests.cs`
- Test auto-injection in Awake()
- Test manual injection via InjectWith()
- Test BeforeInjection/AfterInjection hooks

**Estimated Lines:** ~200 lines

---

### Phase 2: SceneManagement Module ❌ TODO

**Goal:** Universal scene loading with data passing between scenes

**New Module:**
- Assembly: `RPGFramework.SceneManagement.Runtime`
- Location: `Assets/_Framework/SceneManagement/Runtime/`

**Files to Create:**
1. `ISceneDispatcher.cs` - Interface for scene navigation
2. `SceneDispatcher.cs` - Scene loading with data passing
3. `SceneLoader.cs` - Async scene loading with progress
4. `SceneData.cs` - Base class for scene transfer data
5. `SceneLoadingProgress.cs` - Loading progress tracking
6. `RPGFramework.SceneManagement.Runtime.asmdef`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Core/SceneManagement/SceneDispatcher.cs`
- `Assets/_MythHunter/Code/Core/SceneManagement/ISceneDispatcher.cs`
- `Assets/_MythHunter/Code/Core/SceneManagement/SceneLoader.cs`

**Features:**
- Load scene with data object
- Async loading with progress callback
- Additive scene loading
- Scene unloading
- Scene stack management (for back navigation)

**Tests:**
- Create `Assets/_Framework/SceneManagement/Tests/SceneManagementTests.cs`
- Test scene loading with data passing
- Test async loading with progress
- Test additive loading

**Dependencies:**
- RPGFramework.Core.Runtime
- Unity.SceneManagement

**Estimated Lines:** ~400 lines

---

### Phase 3: Systems Module ❌ TODO

**Goal:** System registration, execution order, and parallel processing

**New Module:**
- Assembly: `RPGFramework.Systems.Runtime`
- Location: `Assets/_Framework/Systems/Runtime/`

**Files to Create:**
1. `ISystem.cs` - Base system interface
2. `ISystemRegistry.cs` - System registration interface
3. `SystemRegistry.cs` - System management and execution
4. `ParallelSystemRegistry.cs` - Parallel system execution
5. `SystemPriorities.cs` - System priority constants
6. `SystemExecutionPhase.cs` - Execution phase enum (Early, Normal, Late)
7. `TimerSystem.cs` - Universal timer system
8. `EventThrottlerUpdateSystem.cs` - Event throttling
9. `RPGFramework.Systems.Runtime.asmdef`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Systems/Core/SystemRegistry.cs`
- `Assets/_MythHunter/Code/Systems/Core/ParallelSystemRegistry.cs`
- `Assets/_MythHunter/Code/Systems/Core/ISystemRegistry.cs`
- `Assets/_MythHunter/Code/Systems/Core/SystemPriorities.cs`
- `Assets/_MythHunter/Code/Systems/Core/TimerSystem.cs`
- `Assets/_MythHunter/Code/Systems/Core/EventThrottlerUpdateSystem.cs`

**Features:**
- Register systems with priorities
- Execute systems in order
- Parallel system execution (multi-threading)
- System lifecycle (Initialize, Update, Shutdown)
- System dependencies
- Timer system for delays/intervals

**Tests:**
- Create `Assets/_Framework/Systems/Tests/SystemsTests.cs`
- Test system registration and ordering
- Test system execution
- Test parallel execution
- Test timer system

**Dependencies:**
- RPGFramework.Core.Runtime
- RPGFramework.Events.Runtime

**Estimated Lines:** ~600 lines

---

### Phase 4: Resources Module ❌ TODO

**Goal:** Universal resource loading and management

**New Module:**
- Assembly: `RPGFramework.Resources.Runtime`
- Location: `Assets/_Framework/Resources/Runtime/`

**Files to Create:**
1. `IResourceManager.cs` - Resource loading interface
2. `IResourceProvider.cs` - Resource provider interface
3. `ResourceManager.cs` - Resource management implementation
4. `ResourceManagerExtensions.cs` - Extension methods
5. `IPreloadManager.cs` - Preloading interface
6. `PreloadSceneConfig.cs` - ScriptableObject for preload configuration
7. `AutoPreloadConfigurator.cs` - Automatic preload setup
8. `ResourceReference.cs` - Type-safe resource reference
9. `RPGFramework.Resources.Runtime.asmdef`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Resources/Core/ResourceManager.cs`
- `Assets/_MythHunter/Code/Resources/Core/IResourceManager.cs`
- `Assets/_MythHunter/Code/Resources/Core/IResourceProvider.cs`
- `Assets/_MythHunter/Code/Resources/Core/ResourceManagerExtensions.cs`
- `Assets/_MythHunter/Code/Resources/Config/PreloadSceneConfig.cs`
- `Assets/_MythHunter/Code/Resources/Config/AutoPreloadConfigurator.cs`

**Features:**
- Load from Resources folder
- Load from AssetBundles
- Async loading with callbacks
- Caching and unloading
- Preload configuration
- Resource pooling integration

**Tests:**
- Create `Assets/_Framework/Resources/Tests/ResourcesTests.cs`
- Test resource loading
- Test async loading
- Test preloading
- Test caching

**Dependencies:**
- RPGFramework.Core.Runtime
- RPGFramework.Pooling.Runtime

**Estimated Lines:** ~500 lines

---

### Phase 5: Debug Module ❌ TODO

**Goal:** Runtime debugging and performance monitoring tools

**New Module:**
- Assembly: `RPGFramework.Debug.Runtime`
- Location: `Assets/_Framework/Debug/Runtime/`

**Files to Create:**
1. `DebugToolBase.cs` - Base class for debug tools
2. `DebugDashboard.cs` - Main debug UI
3. `PerformanceMonitor.cs` - FPS, memory, draw calls
4. `SystemProfiler.cs` - System execution profiling
5. `EventDebugTool.cs` - Event monitoring
6. `PoolDebugTool.cs` - Pool statistics
7. `DebugSettings.cs` - Debug configuration
8. `DebugConsole.cs` - In-game console
9. `RPGFramework.Debug.Runtime.asmdef`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Debug/Core/DebugToolBase.cs`
- `Assets/_MythHunter/Code/Debug/UI/DebugDashboard.cs`
- `Assets/_MythHunter/Code/Debug/UI/PerformanceMonitor.cs`
- `Assets/_MythHunter/Code/Debug/Profiling/SystemProfiler.cs`
- `Assets/_MythHunter/Code/Debug/Events/EventDebugTool.cs`
- `Assets/_MythHunter/Code/Debug/Pool/PoolDebugTool.cs`

**Features:**
- Runtime debug dashboard (toggle with F12)
- Performance metrics (FPS, memory, GC)
- System execution profiling
- Event monitoring (counts, frequency)
- Pool statistics
- Debug console with commands
- Conditional compilation (ENABLE_DEBUG)

**Tests:**
- Create `Assets/_Framework/Debug/Tests/DebugTests.cs`
- Test performance monitoring
- Test profiler
- Test debug console commands

**Dependencies:**
- RPGFramework.Core.Runtime
- RPGFramework.Events.Runtime (optional)
- RPGFramework.Pooling.Runtime (optional)
- RPGFramework.Systems.Runtime (optional)

**Estimated Lines:** ~800 lines

---

### Phase 6: Time Module ❌ TODO

**Goal:** Universal timer and scheduling system

**New Module:**
- Assembly: `RPGFramework.Time.Runtime`
- Location: `Assets/_Framework/Time/Runtime/`

**Files to Create:**
1. `ITimer.cs` - Timer interface
2. `ITimerService.cs` - Timer service interface
3. `Timer.cs` - Timer implementation
4. `TimerService.cs` - Central timer management
5. `TimerHandle.cs` - Handle for cancelling timers
6. `TimerMode.cs` - Enum (OneShot, Repeating)
7. `ScheduledAction.cs` - Scheduled action data
8. `RPGFramework.Time.Runtime.asmdef`

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Services/Timer/TimerService.cs`
- Related timer infrastructure

**Features:**
- One-shot timers (delay then execute)
- Repeating timers (interval execution)
- Pausable timers
- Time scale support (scaled/unscaled time)
- Timer cancellation via handle
- Timer pooling (reuse timer objects)

**Tests:**
- Create `Assets/_Framework/Time/Tests/TimeTests.cs`
- Test one-shot timers
- Test repeating timers
- Test timer cancellation
- Test time scale

**Dependencies:**
- RPGFramework.Core.Runtime

**Estimated Lines:** ~300 lines

---

### Phase 7: Pooling Enhancement ❌ TODO

**Goal:** Enhance existing Pooling module with advanced features from GameLayer

**Existing Module:**
- Assembly: `RPGFramework.Pooling.Runtime` (EXISTS)
- Location: `Assets/_Framework/Pooling/Runtime/`

**Files to Add/Enhance:**
1. `PooledObjectLifetimeTracker.cs` - Track pooled object lifetimes
2. `AutoPooledObject.cs` - Auto-return to pool after duration
3. `ScenePoolManager.cs` - Scene-specific pooling
4. `UIPoolPlacement.cs` - UI-specific pool parent management
5. Enhanced `PoolManager.cs` - Add scene tracking

**Source Files (GameLayer):**
- `Assets/_MythHunter/Code/Resources/Pool/PoolManager.cs`
- `Assets/_MythHunter/Code/Resources/Pool/GameObjectPool.cs`
- `Assets/_MythHunter/Code/Resources/Pool/GenericObjectPool.cs`
- `Assets/_MythHunter/Code/Resources/Pool/IObjectPool.cs`
- `Assets/_MythHunter/Code/Resources/Pool/IPoolManager.cs`
- `Assets/_MythHunter/Code/Resources/Pool/PooledObject.cs`
- `Assets/_MythHunter/Code/Resources/Pool/PooledObjectLifetimeTracker.cs`
- `Assets/_MythHunter/Code/Resources/Pool/AutoPooledObject.cs`

**New Features:**
- Scene-based pool isolation
- Lifetime tracking and auto-return
- UI-specific pool placement
- Pool statistics and debugging
- Warm-up pools (pre-create instances)

**Tests:**
- Enhance `Assets/_Framework/Pooling/Tests/PoolingTests.cs`
- Test lifetime tracking
- Test scene isolation
- Test UI placement

**Dependencies:**
- RPGFramework.Core.Runtime
- (No new dependencies)

**Estimated Lines:** ~400 lines

---

## Implementation Order

### ✅ DONE (Before EPIC 25)
1. UI Framework extraction (EPIC 24)
2. Inspector Tooling (Quest Editor, Loot Designer, Item Manager)

### 🚀 EPIC 25 Execution Order

**Week 1: DI & SceneManagement**
1. ✅ Phase 1: DI Enhancement (~200 lines)
2. Phase 2: SceneManagement Module (~400 lines)

**Week 2: Systems & Resources**
3. Phase 3: Systems Module (~600 lines)
4. Phase 4: Resources Module (~500 lines)

**Week 3: Debug & Time**
5. Phase 5: Debug Module (~800 lines)
6. Phase 6: Time Module (~300 lines)

**Week 4: Pooling & Testing**
7. Phase 7: Pooling Enhancement (~400 lines)
8. Integration testing across all new modules
9. Documentation updates

---

## Expected Results

**New Assemblies:** 5 new modules
- RPGFramework.SceneManagement.Runtime
- RPGFramework.Systems.Runtime
- RPGFramework.Resources.Runtime
- RPGFramework.Debug.Runtime
- RPGFramework.Time.Runtime

**Enhanced Assemblies:** 2 enhanced modules
- RPGFramework.DI.Runtime (LazyMonoBehaviour support)
- RPGFramework.Pooling.Runtime (advanced features)

**Total Code:** ~3,200 lines of infrastructure code

**Framework Status After EPIC 25:**
- **40 runtime assemblies** (35 current + 5 new)
- **100% self-contained infrastructure**
- **Production-ready for Asset Store**

---

## Validation Criteria

### Phase Completion Checklist

Each phase must complete:
- ✅ All files created
- ✅ Assembly definition created
- ✅ Tests written and passing
- ✅ Documentation added (XML comments)
- ✅ Integration with existing Framework verified
- ✅ No Unity dependencies in Core (where applicable)

### EPIC 25 Completion Criteria

- ✅ All 7 phases complete
- ✅ All tests passing
- ✅ Framework compiles with no errors
- ✅ Integration test validates cross-module functionality
- ✅ STATUS.md updated in roadmap repository
- ✅ HANDOFF.md updated for next session

---

## Risks and Mitigation

### Risk 1: Breaking Changes to Existing Framework
**Mitigation:**
- Create new modules without modifying existing ones
- Use interfaces for abstraction
- Maintain backward compatibility

### Risk 2: Unity Version Dependencies
**Mitigation:**
- Use Unity 6000.0.45f1 APIs only
- Mark Unity-specific code clearly
- Provide fallbacks where needed

### Risk 3: Performance Impact
**Mitigation:**
- Profile all systems
- Use pooling for allocations
- Benchmark against GameLayer implementation

---

## Documentation Plan

### Per-Module Documentation

Each module gets:
1. **README.md** - Module overview, features, quick start
2. **API.md** - Detailed API documentation
3. **Examples.md** - Code examples
4. **XML Comments** - All public APIs documented

### Overall Documentation

After EPIC 25:
1. Update **Framework Architecture** document
2. Update **Getting Started** guide
3. Create **Infrastructure Guide** (DI, Pooling, Resources, etc.)
4. Update **Asset Store Description**

---

## Next Steps After EPIC 25

### EPIC 26: Enhanced Tooling
- Dialogue Graph Editor
- Ability Tree Editor
- Enhanced Debug Dashboard (Unity Editor window)

### EPIC 27: Reference Game
- "Goblin Valley" demo implementation
- Validates Framework 100% independence
- Sample game as learning resource

### EPIC 28: Asset Store Preparation
- Documentation polish
- Video tutorials
- Marketing materials
- Submission to Asset Store

---

## Current Status (2026-10-25)

**Phase 1 (DI Enhancement):** ⚠️ IN PROGRESS
- Created: `LazyMonoBehaviour.cs` ✅
- TODO: `LazyDependencyInjector.cs`
- TODO: `IDependencyInjector.cs`
- TODO: Tests

**Commit Log:**
- Will commit after Phase 1 complete

**Next Action:**
- Complete Phase 1 (DI Enhancement)
- Then proceed to Phase 2 (SceneManagement)

---

**Created:** 2026-10-25
**Status:** IN PROGRESS (Phase 1 of 7)
**Estimated Completion:** 4 weeks
**Target Framework Version:** 40 assemblies (35 current + 5 new)
