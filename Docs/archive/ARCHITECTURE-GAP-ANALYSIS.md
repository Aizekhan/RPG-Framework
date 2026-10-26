# Архітектурний gap-аналіз: RPG-Framework vs MythHunter

**Дата:** 2026-10-26 · **RPG-Framework:** `0e18ae1` (main) · **MythHunter:** `D:\MythHunter-Git` гілка `dev` @ `bfee98a`

---

## 1. Головний висновок

Під час міграції в RPG-Framework скопіювали **лише `Assets/_Framework`** з MythHunter. Ця копія повна: з неї нічого не загубилося. А **фундамент, на якому реально працює гра**, лежить в іншій папці, `Assets/_MythHunter/Code`: 469 файлів, ~70K рядків, одна збірка `MythHunter.Runtime`. Звідти не перенесли нічого.

| Шар | Рівень зараз | Коментар |
|---|---|---|
| Ігрові модулі (Combat, Quests, Inventory, Stats, …) | **~3 / 5** | Добре розв'язані між собою, 888 тестів |
| Фундамент (події, системи, логування, ресурси, сцени, життєвий цикл) | **~1.5–2 / 5** | Ключових частин немає, а нові незакомічені модулі слабші за версії з MythHunter |

**Що це означає на практиці.** Фреймворк зараз — це *бібліотека ігрових модулів*, а не *фреймворк*. Він не має шляху запуску (bootstrap → composition → DI → цикл оновлення), і модулі не можуть спілкуватися між собою. Кожна нова гра мусить писати весь «клей» сама, а це саме та робота, яку мав би робити фреймворк.

---

## 2. Як була влаштована архітектура MythHunter

```
┌──────────────────────────── Game (MythHunter) ──────────────────────────────┐
│ GameBootstrapper → GameFlowManager → GameStateMachine (Boot/Menu/Lobby/…)  │
│ Composition/Modules + Installers (wiring)      GameLogic/* (19 adapters)   │
├──────────────────── Game infrastructure (_MythHunter/Code) ─────────────────┤
│ Events: IEventBus, EventBus, Throttler, Batcher, EventStore      (58 users) │
│ Systems: SystemRegistry, SystemGroup, PhaseSystemGroup, priorities         │
│ ECS+: SystemBase/ISystemFeatures, ComponentCache, Archetypes, Jobs         │
│ Resources: ResourceManager + providers (Resources/AssetBundle/Addressables)│
│ Pool: injectable PoolManager, PoolMonitor, lifetime/leak tracking          │
│ UI: NavigationService (stack, modals, transitions), ViewFactory, ViewConfig│
│ Logging: IMythLogger/MythLogger (180 users)   Debug: dashboard, profilers  │
│ Async: AsyncTaskTracker, InitializationTransaction, SceneDispatcher (UniTask)│
├────────────────────────── RPGFramework (_Framework) ────────────────────────┤
│ Core.Composition · DI · ECS(EntityManager) · Logging(ILogger only) · …     │
│ 30 gameplay modules                                                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Ключовий момент.** MythHunter вже *спирається* на фреймворк: `RPGFramework.DI` використовується в 72 місцях, `.ECS` — у 14, `.Core.Composition` — у 7. Тобто `_MythHunter/Code` — це надбудова над фреймворком, а не форк. Але в цій надбудові опинилось багато **загальної** інфраструктури, яка за правилами `ARCHITECTURE_RULES.md` (roadmap-репо) мала б жити у фреймворку.

`GameLogic/*` (19 папок, ~17K рядків) — це **адаптери MythHunter поверх модулів фреймворку**, а не дублікати. Вони правильно лишаються в грі.

---

## 3. Чого бракує фреймворку: повний перелік

Позначки: **G** — загальне, переноситься як є; **G\*** — загальне, але треба відв'язати від гри; ⭐ — пріоритет.

### 3.1 Критично: без цього фреймворк не є фреймворком

| # | Компонент | Джерело (MH) | Тип | FW зараз | Що зробити при перенесенні |
|---|---|---|---|---|---|
| ⭐1 | **Event Bus**: `IEvent`, `IEventBus`, `IEventSubscriber`, `EventPriority` | `Code/Events/IEventBus.cs`, `SimpleEventBus.cs` (124), `EventBus.cs` (850) | G\* | **Немає.** 88 типів подій оголошено в 22 модулях, але більшість ніколи не публікується. Persistence, Combat та інші використовують 44 окремі `event Action` | Взяти контракти й **компактну** реалізацію на основі `SimpleEventBus`, а не 850-рядковий `EventBus`. У ньому boxing struct-подій, polling кожні 10 мс, статичний middleware і Lobby-залежності. Перенести 2 набори тестів (DuplicateSubscription, MemoryLeak). Потім підключити модулі, щоб вони реально публікували свої події |
| ⭐2 | **Реалізація логера** | `Code/Utils/Logging/MythLogger.cs` (699) | G | **Лише інтерфейс `ILogger`**, жодної реалізації. Незакомічений `Debug/IDebugLogger` — другий конкурентний контракт | Перейменувати, прибрати статичну фабрику, зробити запис у файл буферизованим. Видалити `IDebugLogger` |
| ⭐3 | **Планувальник систем**: `ISystem`/`SystemBase` (pure C#), `ISystemFeatures` (Update/Fixed/Late/Event), `SystemRegistry`, `SystemGroup`, пріоритети, ізоляція помилок | `Code/Core/ECS/SystemBase.cs`, `ISystemFeatures.cs`, `Code/Systems/Core/*`, `Systems/Groups/*` | G\* | **Слабка заміна** (незакомічений `Systems/`): MonoBehaviour-синглтон з `FindObjectOfType`, без порядку виконання, стадій і груп | Замінити незакомічений `Systems/`. Прибрати enum `GamePhase` і категорії Lobby/Menu |
| ⭐4 | **Gating систем за станом гри**: `IPhaseProvider` (string id), `PhaseSystemGroup`, `IPhaseFilteredSystem` | `Code/Systems/Groups/*`, `Systems/Phase/IPhaseProvider.cs` | G\* | Немає | Лишити лише шлях через string-id. Зв'язати з модулем StateMachine (exploration / combat / cutscene) |
| ⭐5 | **Життєвий цикл сервісів і bootstrap**: зв'язок Core.Composition ↔ DI ↔ системи, `InitializationTransaction` (rollback), `AsyncTaskTracker` | `Code/Core/Initialization/*`, `Core/Async/*`, патерн `GameBootstrapper` | G | `GameCompositionBuilder` не пов'язаний з DI. `FrameworkCoreModule` порожній. Немає `IInitializable`/`ITickable`/`IDisposable` | Написати еталонний bootstrap у фреймворку й перевірити його на `ReferenceGame` |

### 3.2 Важливо: потрібно будь-якій RPG

| # | Компонент | Джерело (MH) | Тип | FW зараз |
|---|---|---|---|---|
| 6 | **Навігація UI**: стек екранів, back, модальні вікна з результатом, переходи, `UIViewFactory`, `ViewConfig` (SO) | `Code/UI/Navigation/*` (~1.1K), `UIViewFactory`, `ViewConfigRegistry` | G\* | Немає: модуль UI вміє лише register/show/hide за закритим enum `ViewId` |
| 7 | **ResourceManager із провайдерами** (Resources / AssetBundle / Addressables за пріоритетом) | `Code/Resources/Core/*`, `Providers/*` | G | Незакомічений `ResourceLoader`: coroutine-обгортка над `Resources.Load`. Addressables винести в окремий asmdef |
| 8 | **Pooling для Unity-об'єктів, через DI**, з `PoolMonitor` і виявленням витоків | `Code/Resources/Pool/*` (~1.3K) | G | Незакомічений `PoolManager` — синглтон. Крім того, він зробив pure-C# Pooling залежним від Unity |
| 9 | **ECS-доповнення**: `ComponentCache<T>` (thread-safe, з тестами), `ComponentCacheRegistry`, **Archetypes** (шаблони сутностей) | `Code/Core/ECS/ComponentCache*.cs`, `Code/Entities/Archetypes/*` | G / G\* | ECS має лише `EntityManager`, без запитів, кешу і шаблонів |
| 10 | **Async-сцени**: `SceneDispatcher` | `Code/Core/SceneManagement/*` | G\* | Незакомічений `SceneLoader`: coroutine + callbacks, Unity-типи прямо в контракті |
| 11 | **Таймери** (іменовані, з паузою, callback) | `Code/Services/Timer/TimerService.cs` | G\* | Незакомічений `Time/` вміє лише паузу і `timeScale` |
| 12 | **Async-стани + контекст** для StateMachine | `Code/Core/StateMachine/BaseState.cs` | G | StateMachine синхронна, без контексту |

### 3.3 Корисно: дешево перенести

| Компонент | Джерело (MH) | Примітка |
|---|---|---|
| Тести DI (36 характеризаційних + 6 продуктивності) | `Tests/DI/DIContainerCharacterizationTests.cs` (1286), `Tests/Runtime/DIContainerPerformanceTests.cs` | Вже тестують `RPGFramework.DI` через reflection, переносяться майже без змін. У FW зараз лише 13 тестів DI |
| `DependencyValidator` | `Code/Core/Validation/` | Валідація графа DI |
| `Ensure`/`Validator`, extensions | `Code/Utils/*` | Дрібні helpers |
| Debug-інструменти: dashboard, `PerformanceMonitor`, `SystemProfiler`, `EventStore` | `Code/Debug/*`, `Events/Debugging/*` | Після Events і систем |
| `LootTableBuilder` (fluent) | `GameLogic/Loot/Utilities/LootTableBuilder.cs` | Залежить лише від `RPGFramework.Loot` |
| Абстрактний `SaveGameCoordinator` (quick-save, автозбереження) | `GameLogic/Persistence/SaveGameCoordinator.cs` | Через hook `ISaveDataProvider` |
| `NetworkAuthority`, кілька обробників на одне повідомлення | `Code/Networking/*` | Для модуля Networking |
| Контракти Cloud (auth, analytics), `IReplaySystem` | `Code/Cloud`, `Code/Replay` | Лише інтерфейси, без перевіреного використання. Відкласти |

### 3.4 Не переносити: лишається в грі

`GameFlowManager`, `GameStateMachine` і стани Boot/Lobby/Menu/Profile, `Composition/Modules` + `Installers`, `PreloadManager`, `Events/Domain` (Lobby, Hero, Rune, Phase), `NetworkEvents`, `Heroes/*`, `Components/*` (крім 5–6 тривіальних), `UIManager`, усі `GameLogic/*`, `EventPool` (марний: boxing struct-подій).

---

## 4. Незакомічені модулі EPIC 25: рекомендація

`Camera`, `Debug`, `Resources`, `SceneManagement`, `Systems`, `Time`, `Pooling/PoolManager` — кожен по 100–200 рядків, тестів 0. Вони **слабші за вже наявні аналоги в MythHunter** і суперечать правилам roadmap. Конкретно:

- Unity-типи прямо в контрактах.
- Статичні синглтони.
- `FindObjectOfType`.
- Namespace `RPGFramework.Time/Debug/Resources` перекривають `UnityEngine.Time/Debug/Resources`.

**Рекомендація:** не комітити їх у поточному вигляді. Їхнє місце займуть перенесені з MythHunter компоненти з п. 3.1–3.2. Корисні дрібниці (`DebugDraw`, Camera) переглянути окремо.

---

## 5. Ще одна системна прогалина: модулі не «знають» один одного

- `Quests` має `ObjectiveType.Kill`, але жодного зв'язку з `Combat.OnDeath`.
- `Combat` не використовує `Stats`.
- `Abilities` з'єднується через сирі делегати `Action<int,string,float>`.

Модулі добре розв'язані між собою. Це правильно, але **точки інтеграції не реалізовані**. Після появи Event Bus треба:
1. Щоб модулі публікували свої оголошені події: Quests (5), Inventory (4), Abilities (4), Progression (4), Guilds (8) тощо.
2. Додати опційні «мости» (Integration-збірки, наприклад `Combat→Quests` kill-objective), які гра вмикає однією реєстрацією.

---

## 6. План міграції (порядок за залежностями)

| Фаза | Що | Залежить від |
|---|---|---|
| **0. Рішення** | Async-модель: **UniTask** (як у MythHunter, ~42 файли) чи Unity 6 **`Awaitable`/`Task`** | — |
| **1. Логування** | Реалізація `ILogger` (з `MythLogger`), видалити `IDebugLogger` | — |
| **2. Основи** | `Ensure`, `InitializationTransaction`, `AsyncTaskTracker`, тести DI + `DependencyValidator` | 0, 1 |
| **3. Події** ⭐ | `RPGFramework.Events`: контракти + компактна шина + тести. Модулі починають публікувати події | 1 |
| **4. Системи** ⭐ | Pure-C# `SystemBase`, `SystemRegistry`, групи, фази. Замінює незакомічений `Systems/` | 3 |
| **5. Bootstrap** ⭐ | Composition ↔ DI ↔ системи, життєвий цикл; еталон у `ReferenceGame` | 2, 4 |
| **6. ECS+** | `ComponentCache`, Archetypes | 3, 4 |
| **7. Unity-адаптери** | Pool, ResourceManager + providers, async-сцени, таймери — в окремих `*.Unity` asmdef | 1, 5 |
| **8. UI навігація** | NavigationService, ViewFactory, ViewConfig | 3, 7 |
| **9. Інструменти** | Debug dashboard, profilers, EventStore; інтеграційні мости між модулями | 3, 4 |

**Після фази 5** фреймворк уперше стане «фреймворком»: гра зможе запуститися, скласти модулі, оновлювати системи і обмінюватися подіями без власного клею.
