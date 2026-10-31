# RPG Framework — архітектура (фактичний стан v0.22.0)

> **Що це за документ.** Опис фреймворку таким, яким він **є** у версії `com.aizekhan.rpg-framework` **v0.22.0**
> (тег `v0.22.0` на коміті `b106c53`, Unity 6000.0), звірений із кодом пакета 2026-10-31: збірки (`.asmdef`), їхні
> посилання, публічні контракти, `README.md`, `CHANGELOG.md` (0.5.1 → 0.22.0), `Documentation~/networking.md`, рецепти
> AI-guidance 01–17.
> Попередня редакція (стан v0.5.0/v0.5.1) у git не потрапляла; цей файл замінює її повністю. Історію змін між
> версіями дає `CHANGELOG.md` пакета.
>
> **Для кого.** Для автора геймдизайн-документа і для того, хто пише гру: що фреймворк уже дає, як ним
> користуватись, де межі. Кожне твердження нижче можна перевірити в пакеті (`Library/PackageCache/com.aizekhan.rpg-framework*`).
> Позначки: **[є]** — зроблено і покрито тестами пакета; **[частково]** — є, але з відомими обмеженнями;
> **[немає]** — відсутнє, для гри це «framework gap». «з vX» — версія, у якій з'явилось.
>
> **Чого тут немає.** Архітектури конкретної гри (її контент, арт, конвеєр рендерингу, бюджети, Game Bible). Це живе в
> репозиторії гри; приклад — `D:\HuntersRunner\Docs\ARCHITECTURE.md` (MythRunner).
>
> **Правило змін.** Документ описує конкретну версію. Під час оновлення фреймворку (новий тег) документ
> звіряється з `CHANGELOG.md` нової версії й оновлюється окремим комітом.

---

## 1. Що таке фреймворк

**RPG Framework = фундамент + ігрові модулі + жанрові кіти + інструменти для гри та AI-асистента.**

- Фреймворк володіє **правилами й інфраструктурою**: композиція, DI, події, системи, ECS, таймери, кубики, бойові
  правила, стати, прогресія, квести, предмети, лут, збереження, ассети, спавн, сцени, UI-навігація, звук, локалізація,
  налаштування, діагностика, мережа.
- Гра володіє **контентом, поданням і правилами, які роблять її унікальною**.
- Розповсюджується як **UPM-пакет із git-тегом** (`#v0.22.0`), ліцензія MIT; залежить від UniTask 2.5.10, Input System
  1.11.2, Unity Transport 2.5.3, uGUI, вбудованого модуля Audio. Гра живе в окремому репозиторії/проєкті і не редагує
  пакет (`Library/PackageCache` перезаписується).
- **Конвеєр рендерингу — будь-який.** Пакет не залежить від URP/HDRP і не має шейдерів під конкретний конвеєр:
  `StarterContent` (New Game) бере шейдер з `GraphicsSettings.currentRenderPipeline.defaultMaterial`, без конвеєра —
  `Standard`; `DiagnosticsOverlay` і `RenderSection` працюють і в Built-in, і в URP. Ігри можуть бути на URP
  (MythRunner перейшов на URP 17 2026-10-31). Налаштування URP у New Game [немає] — це робить гра.

Що вже можна отримати «з коробки» (README, розділ Genres):

| Жанр | Що дає фреймворк | Старт |
|---|---|---|
| Action RPG у реальному часі (вид згори або від третьої особи, **3D**) | **Грабельна гра**: герой, вороги (рівні, еліти), хвилі, лут з афіксами, спорядження, квести, діалоги, крафт, дерева навичок, репутація, торгівля, досягнення, HUD, збереження, **кооп 2–8 гравців** з лобі, PvP — усе з інспекторних ассетів | Game Kit → **New Game** (рецепт 01) |
| Покрокова партійна RPG (JRPG) | Правила (тести є): черга ходів, атаки, критичні удари, стихії, здібності, захист, предмети, втеча, статус-ефекти, AI ворогів, нагороди, рівні, квести, навички, репутація, досягнення, серія боїв, збереження. **Готовий хост і екран бою** з v0.19 (`TurnBasedHost`, `BattleScreen`, `BattleMenu`) | `TurnBasedHost.ConfigureGame` або `TurnBasedComposition.Configure` (рецепт 15) |
| Покрокова гра по мережі (настільна, карткова) | Сесія ходів `RPGFramework.Turns` (з v0.14): хост перевіряє ходи, кожне місце бачить свій вид, вікна реакцій, повернення в гру; кубики й колоди | `TurnHost` + свої `ITurnRules` |
| Інше (roguelike, наратив, survival, раннер…) | Ігрові модулі, core-сервіси (рецепт 14), інший жанр на кіті ActionRpg (рецепт 17), архітектурні правила | New Game, далі замінити те, що не підходить |

---

## 2. Шари

```
┌──────────────────────────────────────────────────────────────────────────┐
│ GAME (окремий проєкт; <Game>.Core без UnityEngine + <Game> з поданням)   │
│   контент (SO), правила гри, модулі гри, view/input/HUD, адаптери        │
├──────────────────────────────────────────────────────────────────────────┤
│ GENRE KITS                                                               │
│   ActionRpg (.Runtime pure C# + .Unity + .Editor)                        │
│   TurnBased (.Runtime pure C# + .Unity)  ·  Turns (.Runtime, сесія ходів)│
├──────────────────────────────────────────────────────────────────────────┤
│ COMPOSITION OF GAMEPLAY                                                  │
│   Bootstrap.Gameplay (готові feature-модулі) · Bootstrap.UnityServices   │
├──────────────────────────────────────────────────────────────────────────┤
│ INTEGRATION BRIDGES:  Integration.CombatQuests · .StatusEffectsStats     │
├──────────────────────────────────────────────────────────────────────────┤
│ GAMEPLAY MODULES (L2)                                                    │
│   Stats · Combat · StatusEffects · Abilities · Progression · Quests ·    │
│   Items · Inventory · Loot · Persistence · Dialogue · Crafting · Skills ·│
│   Trading · Factions · Achievements · Interaction · Pathfinding ·        │
│   BehaviorTree · Characters · Dice · Cards · Guilds · Housing ·          │
│   Companions · DynamicEvents · Environment                               │
├──────────────────────────────────────────────────────────────────────────┤
│ NETWORKING:  Networking (pure C#) · Networking.UnityTransport ·          │
│              Networking.UnityRelay (опційно) · DeltaCompression          │
├──────────────────────────────────────────────────────────────────────────┤
│ UNITY LAYER (L1): Bootstrap.Unity · AssetManagement.Unity/.Addressables ·│
│   Pooling.Unity · Spawning.Unity · Presentation.Unity · Scenes.Unity ·   │
│   Timers.Unity · Logging.Unity · Cameras.Unity · UINavigation.Unity · UI │
│   · Audio.Unity · Localization.Unity · Settings.Unity · Cards.Unity ·    │
│   Diagnostics.Unity · Tools.* · Editor · GameKit                         │
├──────────────────────────────────────────────────────────────────────────┤
│ FOUNDATION (L0): Logging · DI · Core · Events · ECS · Systems · Timers · │
│   StateMachine · Serialization · Pooling · Bootstrap(GameHost) ·         │
│   Audio · Localization · Settings (pure C#) · AssetManagement/Scenes/    │
│   UINavigation (контракти) · Diagnostics.Runtime                         │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Правила залежностей (як є)

1. Залежності йдуть вниз. Фреймворк **ніколи** не посилається на збірку гри (правило README).
2. Модуль говорить з іншим модулем лише через контракти чи події. Прямі посилання між L2 — лише ті, що в п. 5.3.
3. Логіка, незалежна від рушія, — у `Runtime`; Unity-код — у `Unity`; тести — у `Tests`.
4. У player-білді немає `UnityEditor` поза `#if UNITY_EDITOR` і editor-збірками.
5. Жанровий кіт — єдине місце фреймворку, яке знає багато модулів одразу і з'єднує їх (`ActionRpg.Runtime`
   посилається на 29 збірок, `TurnBased.Runtime` — на 23).

### 2.2 Імена збірок і namespace

- Збірка: `RPGFramework.<Module>.Runtime` / `.Unity` / `.Tests` (у коді `asmdef` суфікс `.Runtime` **є**).
- Namespace здебільшого `RPGFramework.<Module>` без суфікса. **Винятки** (технічний борг, див. розд. 12):
  `RPGFramework.Inventory.Runtime`, `.Guilds.Runtime`, `.Companions.Runtime`, `.DynamicEvents.Runtime`,
  `.Environment.Runtime`, `.Achievements.Runtime` (частина), `.Housing.Runtime.*`, `RPGFramework.Core.Composition`.
- Namespaces `RPGFramework.Time`, `.Debug`, `.Resources` не використовуються (перекривали б `UnityEngine.*`):
  є `Timers`, `Diagnostics`, `AssetManagement`.

---

## 3. Foundation

| Модуль | Збірка | Обов'язок | Ключові контракти | Стан |
|---|---|---|---|---|
| **Logging** | `Logging.Runtime` (noEngine) + `Logging.Unity` | Логер | `ILogger`, `LogSeverity`, `Logger`, `CategoryLogger`, sinks `FileLogSink` / `MemoryLogSink` / `ILogSink`, `NullLogger` | [є] |
| **DI** | `DI.Runtime` | Контейнер, scopes, ін'єкція, валідація графа | `IDIContainer` (`Register*`, `RegisterFactory`, `RegisterLazySingleton`, `RegisterScoped`, `Resolve`, `Validate`, `CreateScope`), `[Inject]`, `DIInstaller`, `LazyDependency<T>`, `DIValidationResult` | [є] |
| **Core** | `Core.Runtime` | Композиція модулів, транзакція ініціалізації, async-трекер, пауза, гаманець | `IFeatureModule`, `BaseFeatureModule`, `IGameCompositionBuilder`, `GameCompositionBuilder`, `IGameComposition`, `InitializationTransaction`, `AsyncTaskTracker`, `AsyncSerialQueue`, `Ensure`; `IGamePause` (`Pause(reason)`, `Resume(reason)`, `ResumeAll`, з v0.8); `ICurrencyWallet` / `CurrencyWallet` (з v0.6) | [є] |
| **Events** | `Events.Runtime` | Шина подій | `IEvent`, `IEventPublisher`, `IEventBus`, `EventPriority`, `EventBus`, `SubscriptionBag`, `EventThrottler`, `EventBatcher`, `EventRecorder` | [є] |
| **ECS** | `ECS.Runtime` (noEngine) | Сутності, компоненти, архетипи | `IEntityManager` (у т.ч. `GetEntitiesWith<T1,T2>(List<int>)` без алокацій), `IComponent`, `ArchetypeCatalog`, `ArchetypeBuilder`, `EntityArchetype` | [є] |
| **Systems** | `Systems.Runtime` | Світ, реєстр і стадії систем, фази | `IWorld` / `World` (опційний `IGamePause`: на паузі системи отримують delta 0), `ISystem`, `SystemBase`, `IUpdateSystem` / `IFixedUpdateSystem` / `ILateUpdateSystem`, `ISystemRegistry`, `SystemGroup`, `PhaseSystemGroup`, `IPhaseProvider`, `IPhaseFilteredSystem`, `PhaseTracker`, `PhaseChangedEvent`, `SystemPriorities`, `EventPumpSystem`, `SystemProfiler` | [є] |
| **Timers** | `Timers.Runtime` + `Timers.Unity` | Відкладені й повторювані виклики, масштаб часу | `ITimerService` (`Schedule`, `Repeat`, `Cancel`, `Pause/Resume`, категорії, `DelayAsync`), `TimerHandle`, `ITimeScaleController` | [є] |
| **StateMachine** | `StateMachine.Runtime` | FSM (sync і async) | `IState<TStateId>`, `StateMachine<TStateId>`, `IAsyncState<TStateId>`, `AsyncState<TStateId>`, `AsyncStateMachine<TStateId>`, `StateTransition<TStateId>` | [є] |
| **Serialization** | `Serialization.Runtime` (noEngine) | Версійна серіалізація, міграції | `VersionedSerializer`, `IComponentSerializerRegistry`, `ComponentSerializerBase<T>`, `IMigrationRegistry`, `IMigrator<TFrom,TTo>`, `TypeRegistry` | [є] |
| **Pooling** | `Pooling.Runtime` (noEngine) | Пули plain C# об'єктів | `IObjectPool<T>`, `ObjectPool<T>`, `IPoolManager`, `PoolManager`, `PoolMonitor` (витоки), `PoolStatistics` | [є] |
| **Bootstrap** | `Bootstrap.Runtime` | Хост гри без Unity | `GameHost`, `GameHostState`, `FrameworkCoreModule` (реєструє також `IDiceService`), `FrameworkCoreOptions` (`InitialPhaseId`, `DiceSeed`), `GameFeatureModule` | [є] |
| **Audio** | `Audio.Runtime` (noEngine) + `Audio.Unity` | Звук: кюї, музика, гучності | див. розд. 4.1 | [є] з v0.12 |
| **Localization** | `Localization.Runtime` (noEngine) + `Localization.Unity` | Тексти кількома мовами | див. розд. 4.2 | [є] з v0.17 |
| **Settings** | `Settings.Runtime` (noEngine) + `Settings.Unity` | Налаштування гравця | див. розд. 4.3 | [є] з v0.18 |

### 3.1 Events — фактичний API

```csharp
public interface IEventPublisher
{
    void Publish<TEvent>(TEvent e) where TEvent : IEvent;          // синхронно
    void PublishSticky<TEvent>(TEvent e) where TEvent : IEvent;    // запам'ятовується для пізніх підписників
    UniTask PublishAsync<TEvent>(TEvent e, CancellationToken ct = default) where TEvent : IEvent;
    void Enqueue<TEvent>(TEvent e) where TEvent : IEvent;          // відкладено, до Flush
}
public interface IEventBus : IEventPublisher
{
    IDisposable Subscribe<TEvent>(Action<TEvent> h, EventPriority p = Normal, bool receiveSticky = true);
    IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> h, EventPriority p = Normal);
    bool Unsubscribe<TEvent>(...); bool UnsubscribeAsync<TEvent>(...);
    int Flush(int maxEvents = int.MaxValue); int PendingCount { get; }
    bool TryGetSticky<TEvent>(out TEvent e); bool ClearSticky<TEvent>();
    int GetSubscriberCount<TEvent>(); IDisposable AddObserver(Action<Type, object> observer); void Clear();
}
```

- Обмеження в API — `where TEvent : IEvent` (не `struct`). Те, що події — `readonly struct` з get-only
  властивостями, **для гри перевіряє GA07**; у самому фреймворку це конвенція.
- Відкладені події розбирає `EventPumpSystem`, яку `FrameworkCoreModule` реєструє в реєстрі систем.
- Підписка повертає `IDisposable`; для набору підписок — `SubscriptionBag`.
- Модулі приймають опційний `IEventPublisher` останнім параметром конструктора; з v0.18 Crafting і Persistence теж
  публікують на шину гри (`CraftingStarted/Completed/…Event`, `SaveCreated/Loaded/…Event`).
- Бенчмарк пакета: 1000 подій × 3 підписники ≈ 0,13 мс, без сміття.

### 3.2 Systems і World

- `ISystem` — plain C#. Стадії: система реалізує `IUpdateSystem`, `IFixedUpdateSystem`, `ILateUpdateSystem`.
- Порядок — пріоритет при реєстрації (`SystemPriorities.Gameplay` тощо; `GameFeatureModule.AddSystem<T>(priority)`).
- **Фази гри** — рядкові id: `PhaseTracker` (реєструється ядром, початкова фаза — `FrameworkCoreOptions.InitialPhaseId`)
  публікує `PhaseChangedEvent`; система з `IPhaseFilteredSystem` працює лише у своїх фазах; `PhaseSystemGroup`.
- **Пауза** (з v0.8): `IGamePause` з причинами — гра йде далі, коли не лишилось жодної (меню й голосування не
  скасовують одне одного); `World.IsPaused`, `World.Time` стоїть; таймери, ефекти, кулдауни, хвилі стоять; мережа працює.
- Кожен `GameHost` має свій світ, тож кілька світів в одному процесі можливі (так тестується кооп: хост і клієнти в
  одному EditMode-тесті).
- Бенчмарк: 10 000 сутностей, 1000 рухомих ≈ 0,24 мс/кадр без сміття.

### 3.3 Життєвий цикл (фактичний)

```
GameHost.Start(configure)                               // Bootstrap.Runtime, без Unity
  1. DIContainer (або переданий), RegisterInstance(ILogger)
  2. builder.UseModule(new FrameworkCoreModule(options))
       → IEventBus/IEventPublisher, EventThrottler, EventBatcher, IEntityManager, PhaseTracker/IPhaseProvider,
         ITimerService, IDiceService, ISystemRegistry, IWorld; EventPumpSystem і система таймерів у реєстрі
  3. configure(builder)                                  // гра / жанровий кіт додає модулі
  4. builder.Build():
       a. топологічне сортування за IFeatureModule.Dependencies (цикл або відсутній модуль → помилка)
       b. module.Configure(IDIContainer) для кожного
       c. container.Validate().ThrowIfInvalid()          // граф DI повний, до ініціалізації
       d. InitializationTransaction: module.Initialize() по черзі; помилка → Shutdown уже ініціалізованих
  5. World = Resolve<IWorld>(); World.Initialize(); State = Running
  помилка будь-де → State = Failed, усе зупиняється, виняток прокидається далі

Цикл:   GameHostBehaviour (Bootstrap.Unity, MonoBehaviour) → host.Update / FixedUpdate / LateUpdate
Кінець: host.Dispose() — безпечно в будь-якому стані і повторно
```

- `GameFeatureModule` (база для модулів гри): `Dependencies` = `FrameworkCoreModule`, `Container`,
  `AddSystem<T>()`, `OnInitialize` / `OnShutdown`; при Shutdown знімає свої системи у зворотному порядку.
- **Асинхронної ініціалізації модулів немає** (немає `IAsyncInitializable`); async — через UniTask у сервісах
  (`IAssetManager.LoadAsync`, `ISceneDispatcher`, `ITimerService.DelayAsync`, `AsyncStateMachine`).

---

## 4. Unity layer і core-сервіси

`UnityServicesModule` (`Bootstrap.UnityServices`) реєструє спільні сервіси; `ActionRpgHost` додає його сам.

| Потреба | Сервіс | Збірка | Замість (GA13) |
|---|---|---|---|
| Спавн/деспавн багатьох об'єктів | `IPrefabSpawner` (`ConfigurePool`, `SpawnAsync`, `Despawn`), `PrefabCatalog` | `Spawning.Unity` | `Instantiate`, `Destroy` |
| Пул GameObject / plain-об'єктів | `IPoolManager` + `ObjectPool<T>`; Unity-пули в `Pooling.Unity` (переживають завантаження сцени з v0.5.1) | `Pooling.*` | `Stack<GameObject>`, свій pool |
| Ассети | `IAssetManager.LoadAsync<T>` (handle звільняти); провайдери Resources, Addressables (`AssetManagement.Addressables`, компілюється лише з `com.unity.addressables`, define `RPG_ADDRESSABLES`) | `AssetManagement.*` | `Resources.Load` |
| Сцени | `ISceneDispatcher` (події на шині; пули сцени звільняються при вивантаженні) | `Scenes.*` | `SceneManager.LoadScene` |
| Сутність ↔ GameObject | `IEntityViewRegistry` | `Presentation.Unity` | `FindObjectOfType`, `GameObject.Find` |
| Екрани UI | `INavigationService` (стек екранів, модальні вікна, переходи `CanvasGroupTransition`), `PrefabViewFactory`, `CodeViewFactory` (екрани з коду, без префабів, з v0.7) | `UINavigation.*` | ручне вмикання панелей |
| Готові RPG-панелі й HUD | `UI.Runtime`: `UI.Panels`, `UI.HUD`, `IPresenter`; з коду — `StatListPanel`, `EquipmentSlotsPanel`, `ActionListPanel` (рядки з кнопками, прокрутка `MaxHeight`, `Anchor`/`Offset`), `ItemSlotUI.Create`, `UIBuilder`; `HealthTrail` (слід удару на смузі здоров'я, з v0.19) | `UI.Runtime` | — |
| Звук | `IAudioService` (розд. 4.1) | `Audio.*` | `PlayOneShot`, `PlayClipAtPoint` (GA13 з v0.12) |
| Тексти й мови | `ILocalization` (розд. 4.2) | `Localization.*` | рядки в коді |
| Налаштування гравця | `ISettingsService` (розд. 4.3) | `Settings.*` | свої `PlayerPrefs` |
| Камери | `Cameras.Unity` | — | — |
| Діагностика | панель (розд. 4.4) | `Diagnostics.*` | — |
| Логи | `ILogger` (`Logging.Unity` — sink у консоль Unity) | `Logging.*` | `Debug.Log` |
| Затримки, повтори | `ITimerService` | `Timers.*` | `Invoke`, `WaitForSeconds` |
| Кубики | `IDiceService` (`Roll(sides, purpose)`, `Seed`, `NextSeed`, `Log`, `DieRolledEvent`), `ScriptedDice` для тестів (з v0.13) | `Dice.Runtime` | `System.Random` у правилах |
| Збереження | `SaveParticipant<T>` (`Version`, `Upgrade`), `SaveGameCoordinator`, `ISaveSystem` (резервна копія `<slot>.previous.sav`, контрольна сума) | `Persistence.Runtime` | `PlayerPrefs`, свої JSON |
| Редакторські ассети | `Tools.*` (Item Database, Loot Table, Quest, Dialogue, Faction, Skill, Ability, Crafting, Status Effect) — інспектор показує проблеми, конвертація в runtime-дані | `Tools.*` | — |
| Генератори і правила гри | `GameKit` (New Game, New Feature, Check Architecture, AI guidance, CI workflow, `.gitattributes` для LFS) | `GameKit.*` | — |

Повна таблиця «потреба → сервіс» — рецепт 14; «потреба гри → модуль» — рецепт 16 (обидва в `Docs/RPGFramework/recipes/` гри).

### 4.1 Audio (з v0.12)

- `IAudioService` / `AudioService` (pure C#): кюї `AudioCueData` з варіантами кліпів (без повтору поспіль), розкидом
  гучності й тону, 2D/3D (`AudioPosition`), `MaxVoices`, `MinInterval`; `Play(cueId, position)`, `StopAll`;
  `On<TEvent>(cueId, …)` — звук з подій шини; музика з кросфейдом (`PlayMusic(trackId, fade)`, `StopMusic`;
  `MusicTrack.Loop` з v0.14.1); канали гучності `AudioChannels` (master, music, sfx, ui або свої), що зберігаються в
  `IVolumeStore`.
- `IAudioOutput` робить звук: `RecordingAudioOutput` для тестів і серверів; `UnityAudioOutput` (пул `AudioSource`, дві
  музичні доріжки, групи мікшера).
- Unity: ассети `AudioCue`, `AudioLibrary`; `AudioModule` (сервіс на шині хоста, гучності в PlayerPrefs
  `RPGFramework.Audio.<channel>`, реальний час навіть на паузі). `ActionRpgHost` має поле Audio і грає кюї `kit.*`.
- Кооп: звук мережею не ходить, кожна машина грає зі своїх подій.
- Сцена без хоста гри (головне меню): компонент `SceneAudio` (бібліотека, музика на старті, збережені гучності, реальний час, зникає зі сценою; `SceneAudio.Audio`) — [є] з v0.22.

### 4.2 Localization (з v0.17)

- `ILocalization` / `LocalizationService` над `StringTable` (по одній на мову): `Text(key)`, `Format(key, (name, value)…)`
  з іменованими значеннями (`{damage}`, `{value:0.0}` у числах мови), `Plural(key, count)` — `.one/.few/.many/.other`
  за `PluralRules` (англійська, українська/російська/білоруська, польська, чеська/словацька, японська/китайська/корейська;
  `PluralRules.Set`). Відсутній текст — з мови за замовчуванням, далі сам ключ; `MissingKeys`.
- `LocalizedText` (ключ + значення) — текст, який правила на чистому C# кладуть у події, лог, мережу; `LocalizedTextCodec`
  (з v0.19) переносить його в байтах або одним рядком, тож хост і кожен клієнт читають своєю мовою.
- Мова: `SetLanguage` ("uk-UA" знаходить "uk"), `Languages`, мова пристрою на першому старті, `LanguageChangedEvent`.
- `LocalizationCsv` (`key,en,uk` для перекладачів), `LocalizationChecks.Problems` — тест, що тримає переклади цілими.
- Unity: `StringTableAsset` (зі шрифтом мови), `LocalizationLibrary`, `LocalizationModule` (мова в PlayerPrefs).
- Тексти кітів — ключі: `kit.hud.*`, `kit.coop.*`, `kit.log.*`, `kit.menu.*` (`KitTexts.Table()`), `settings.*`,
  `battle.refusal.*` (`BattleTexts.Table()`, з v0.20).
- **Відмови кодами** (з v0.20): `TradeError` на `TradeResultData.Error` / `TradeDeclinedEvent.Error`
  (`InsufficientGold`, `MissingItems`, `ReputationTooLow`, …); `BattleRefusal` / `BattleRefusalReason`
  (`Battle.RefusalOf`, `ActionResult.Reason` / `RefusalText`, `BattleCommand.Why`); `TurnHost.RefusedNoMove` /
  `RefusedNotStarted` / `RefusedBusy` / `RefusedNotYourMove`. Англійські `ErrorMessage` / `Refusal` лишились.

### 4.3 Settings (з v0.18)

- `ISettingsService` / `SettingsService`: типізовані опції (`ToggleSetting`, `ChoiceSetting`, `SliderSetting`), групи,
  `ResetToDefaults(group)`, зберігання через `ISettingsStore` (значення за замовчуванням не пишеться),
  `SettingChangedEvent`.
- `DisplaySettings` (група "Display"): розмір екрана, режим вікна, якість, VSync, ліміт кадрів через `IDisplayOutput`;
  перший старт нічого не змінює.
- Unity: `SettingsModule` (PlayerPrefs `RPGFramework.Settings.<id>`, `UnityDisplayOutput`; гучності й мова, коли є ті
  модулі — `SettingsBridges.AddVolumes`, `AddLanguage`), `SettingsRows.For` — сторінка налаштувань на `ActionListPanel`.
  `ActionRpgHost` 'Settings' (увімкнено за замовчуванням).

### 4.4 Diagnostics

- `DiagnosticsModule` → `DiagnosticsHub` із секціями `IDiagnosticsSection` (системи, події, кадри, пули, таймери);
  `DiagnosticsOverlay` показує їх; не потрапляє в релізний білд.
- **Render** (з v0.21): `RenderSection` рахує трикутники й меші в полі зору камери на CPU (працює без графіки, у тестах),
  по моделях від найважчої (`Heaviest`, `RenderModel`, `ModelOf`), і там, де працюють лічильники профайлера (Editor,
  development build), — GPU-трикутники, draw calls, set-pass, batches. `Measure()` → `RenderSample` для тестів бюджету;
  `RenderBudget` (`Triangles`, `DrawCalls`, `SetPassCalls`, `ModelTriangles`; 0 — без ліміту) — перевищення позначено OVER.
- `DiagnosticsOverlay.ToggleKey` (з v0.21) — клавіша з коду; `ActionRpgHost`: поле Diagnostics Key (F3, None — без
  клавіші), Render Budget, `ActionRpgHost.Render`. Камера, що стежить за гравцем, наближається плавно: тест після
  телепорту чекає, поки вона доїде, інакше рахує старий вид.

---

## 5. Ігрові модулі (L2)

### 5.1 Модулі і їхня зрілість

Зрілість визначає не позначка, а **хто реально використовує модуль**: модуль, на якому стоїть жанровий кіт, перевірений
грабельною грою. «Feature-модуль» — чи можна підключити одним `UseModule(...)` (інакше гра реєструє сервіси сама у
своєму `GameFeatureModule`).

| Модуль | Що вміє | Feature-модуль | Використовує | Ассети дизайнера |
|---|---|---|---|---|
| **Stats** | `StatContainer`, модифікатори з пріоритетом, ріст за рівнем (`IStatGrowthFormula`) | — (через кіти) | ActionRpg, TurnBased | — |
| **Combat** | `LifeState`, шкода (`IDamageCalculator`, `IContextualDamageCalculator` + `AttackContext`: теги, джерело, `AsCritical`, `WithMultiplier`), стихії `DamageResistances` (опір, слабкість, імунітет, змішана шкода; з v0.19), `DeathEvent` | `CombatModule` | ActionRpg, TurnBased | — |
| **StatusEffects** | стаки, тіки (`ProcessTicks` на інтерфейсі з v0.20, наздоганяє пропущені), сила й тривалість застосування, `Reapply`, `UseStatContainers<T>()`, контроль, множник руху | `StatusEffectsModule` | ActionRpg, TurnBased | Status Effect |
| **Abilities** | вартість (і щосекундна — upkeep), кулдауни (`CooldownClearedEvent`), каст, канал з тіками, заряди з відновленням, toggle, пасивні, `TargetType.AllEnemies/AllAllies` | `AbilitiesModule` | ActionRpg, TurnBased | Ability |
| **Progression** | досвід, рівні, `LevelUpEvent` | `ProgressionModule` | ActionRpg, TurnBased | — |
| **Quests** | квести, цілі (Kill, Collect, Explore, Interact…), нагороди, реєстр | `QuestsModule` | ActionRpg, TurnBased | Quest Asset |
| **Items** | предмети, `ItemInstance`, реєстр, ефект споживання; афікси (`AffixPool`, `RolledItems` — предмет з афіксами в id), `RarityTable` з удачею, сети (`ItemSet`) | `ItemsModule` | ActionRpg, TurnBased | Item |
| **Inventory** | контейнери з місткістю, слоти екіпіровки | — | ActionRpg (рюкзак партії або свій у кожного), TurnBased | — |
| **Loot** | таблиці з вагою, без дублікатів, `LootGeneratedEvent` / `LootDroppedEvent` | `LootModule` | ActionRpg, TurnBased | Loot Table |
| **Persistence** | слоти, секції з версіями й апгрейдом, резервна копія, `KeepUnknownSections` | — | ActionRpg, TurnBased | — |
| **Trading** | магазин: пропозиції по гравцях, оплата з `ICurrencyWallet`, предмети через `ITradeInventory` (все або нічого), ціна гравця `ITradePricing`, коди відмов `TradeError` | `TradingModule` | ActionRpg | — |
| **Factions** | фракції, стосунки, репутація з поширенням | — | ActionRpg, TurnBased | Faction Database |
| **Dialogue** | вузли, умови (`IDialogueGameContext`, Custom), вибори, події | — | ActionRpg (`ActorDialogue`) | Dialogue |
| **Crafting** | рецепти, станції, інструменти, вимоги | — | ActionRpg (`ActorCrafting`) | Crafting Recipe |
| **Skills** | дерева, пререквізити | — | ActionRpg (`ActorSkills`), TurnBased (`PartySkills`) | Skill, Skill Tree |
| **Achievements** | критерії, `AchievementTracker` (лічильники, `Capture`/`Restore` як профіль) | — | ActionRpg, TurnBased | — |
| **Interaction** | `InteractionPoint`, `IInteractionService` (відстань — гри), `InteractedEvent` (з v0.7) | `InteractionModule` | ActionRpg | — |
| **Pathfinding** | `GridNavigationGraph`, `AStarPathfinder`, `DistanceMap` (карта відстаней / flow field для багатьох переслідувачів, з v0.18) | — | — | — |
| **Dice** | `IDiceService`, `DiceService` (сід, лог), `ScriptedDice` (з v0.13) | у ядрі | The Bloodline Hunt | — |
| **Cards** | `Deck<T>`, `Hand<T>`, `DeckAsset<TCard>` (з v0.13) | — | The Bloodline Hunt | DeckAsset |
| **BehaviorTree** | `BehaviorTree` і вузли | — | — | — |
| **Characters** | дані персонажа над Stats | — | — | — |
| Guilds, Housing, Companions, DynamicEvents, Environment | правила і події, тести модуля | — | — | — |

Висновок для GDD: модулі без користувача (BehaviorTree, Characters і нижче) мають тести й опубліковані події, але
**жодна гра на них ще не стояла**. Функція гри, що спирається на них, має закласти час на знахідки. Модулі, що з v0.19
підключені до кітів (Dialogue, Crafting, Skills, Achievements, Factions), перевірені тестами кітів, але ще не грою в релізі.

### 5.2 Чого в модулях немає (часті питання дизайну)

| Потреба | Стан |
|---|---|
| Імунітети, слабкості, стихії | [є] з v0.19: `DamageResistances` (Combat), `ActionRpgSettings.ElementTags`, `CharacterData.Resistances`, "resist:<element>" з предметів, ефектів, навичок; TurnBased — `AbilityEffectData.Element`, `CombatantData.Resistances` |
| Особистий інвентар кожного гравця в коопі | [є] з v0.5.0: `ProgressMode.PerPlayer` |
| Здібність по області (AoE) | [частково]: `TargetType.AllEnemies/AllAllies`; по радіусу — у колбеку ефекту (`ActorAbilities.SetEffect`) гра сама обирає цілі |
| Розподіл очок характеристик | [немає], належить грі (модифікатори `StatContainer`); очки навичок — [є] (Skills) |
| Карта відстаней / flow field | [є] з v0.18: `DistanceMap` |
| Фізика 2D (Rigidbody2D, Tilemap) у жанровому кіті | [немає]; ActionRpg — 3D. 2D-вигляд = 3D-сцена з ортографічною камерою зверху |
| Екран бою для TurnBased | [є] з v0.19: `BattleScreen`, `BattleMenu`, `TurnBasedHost`; свій view — на `ITurnBasedWorld` |
| Тексти кількома мовами | [є] з v0.17 (розд. 4.2) |
| Звук у сцені без хоста гри (меню) | [є] з v0.22: `SceneAudio` |
| Налаштування URP / HDRP у New Game | [немає] (розд. 1) |

### 5.3 Прямі залежності між модулями (з `.asmdef`)

```
ECS ← Stats ← Items                      Serialization ← Stats, Combat
ECS ← Combat, StatusEffects, Inventory, Quests, Dialogue, Crafting, Skills, Factions, Achievements, …
Factions ← Guilds          Inventory ← Housing          Events ← майже всі
Localization ← Turns, TurnBased (з v0.19/v0.20)
Abilities, Loot, Progression, Trading — лише Events (без ECS)
Pathfinding, BehaviorTree, Dice, Cards — без залежностей від інших L2
```

Немає прямого зв'язку Stats ← Combat, Stats ← StatusEffects, Combat ← Abilities, Items ← Loot, Items ← Inventory:
зв'язки робить міст (StatusEffects→Stats) або жанровий кіт.

### 5.4 Стандартна структура модуля

```
<Module>/
  Runtime/   RPGFramework.<Module>.Runtime.asmdef; Contracts/ Data/ Events/ Implementations/ (Systems/)
  Unity/     RPGFramework.<Module>.Unity.asmdef — якщо потрібен Unity
  Tests/     RPGFramework.<Module>.Tests.asmdef
```

Feature-модулі для підключення одним рядком лежать централізовано в `Bootstrap.Gameplay`, а не в папці модуля
(виняток — модулі зі своїм Unity-шаром: `AudioModule`, `LocalizationModule`, `SettingsModule` у `<Module>.Unity`).

---

## 6. Мости (Integration)

| Міст | Що робить | Стан |
|---|---|---|
| `Integration.CombatQuests` (`KillObjectiveBridge`, `CombatQuestsModule`) | смерть → прогрес цілі «вбити» | [є] |
| `Integration.StatusEffectsStats` (`StatContainerEffectTarget<TStatType>`) | модифікатори ефектів → `StatContainer` (з v0.18 — `UseStatContainers<T>()` без ручного з'єднання) | [є] |

Інших мостів окремими збірками **немає**. У кітах досвід, лут, квести, досягнення, репутацію за вбивство роздає сам
світ кіта (`ActorWorld`, `TurnBasedWorld`); гра без кіта з'єднує модулі своїм модулем на подіях.

---

## 7. Жанрові кіти

### 7.1 ActionRpg

- **Rules (pure C#, `ActionRpg.Runtime`):** `ActorWorld` / `IActorWorld` — актори, партія, удари з `AttackContext`
  (критичні, множники, ухилення, вампіризм, стихії), `DealDamage`, `Heal`, вороги (`SpawnEnemy`, `StartWave`, рівні
  `EnemyLevels`, еліти `EliteAffixes`), гравці (`AddPlayer`, `RemovePlayer`, `RevivePlayer`), прогрес партії або
  кожного (`ProgressMode`), гаманець і торгівля (`Wallet`, `Trade`, `Traders`), спорядження (`Gear`: слоти, набори на
  героя, бонуси, сети, афікси), `Interactions`, `GiveLoot`, квести (`AcceptQuest`, `CompleteQuest`), підсистеми:
  `ActorEffects`, `ActorAbilities` (`CanUse`, `CooldownFor`, повернення вартості), `Skills`, `Crafting`, `Consumables`,
  `Dialogue`, `Reputation`, `Achievements`; журнал `LogLines` (тексти для перекладу); `ActionRpgModule`,
  `ActionRpgComposition`, `ActionRpgSettings`, `WaveSystem`, `IHostilityRules`, PvP (`PvpMatch`: Duel, TDM, FFA),
  `ActionRpgSaves` (зведення сейва `SaveSummary`).
- **Unity (`ActionRpg.Unity`):** `ActionRpgHost` — хост на сцені: хуки `ConfigureGameModules`, `ConfigureSettings`,
  `ConfigureUnityServices`, `ConfigureSaves`, `ConfigureCoopMessages`, `ConfigureScreens`; `ChangeScene` /
  `OnSceneChanged` / `TeleportPlayer`; старт на запит (`UseStartOnRequest`, `StartGame(GameStart)`, `Launch` з меню),
  `EndGame(sceneName)` (з v0.16.1); `Navigation`, `Localization`, `Settings`, Audio, `Render`; `AbilityDistance`,
  `InteractionDistance` (раннер чи сітка міряє по-своєму). `GameSetup` і `CharacterDefinition` (ассети дизайнера),
  `ActorPresenter`, `ActorView`, `PlayerController` (Input System), `EnemyBrain`, `StrikeAnimator`, `HealthBarView` (зі
  слідом удару), `ActionRpgHud`, `CoopPanel` (F2, лобі), `InteractionPointView`, `KitRows` (сторінки навичок, рецептів,
  витратних, репутації, досягнень, діалогу на `ActionListPanel`).
- **Важливо для дизайну:** **рух і позиції живуть у view** (Unity), правила — у `ActorWorld`; AI ворогів —
  MonoBehaviour `EnemyBrain` (замінюється своїм `ActorView`-нащадком). Інший жанр на цьому кіті (раннер, доріжки,
  сітка) — рецепт 17. GA14 (з v0.6) не дає грі писати вручну лут, ціни, кулдауни й таймери.

### 7.2 TurnBased

- **Rules (`TurnBased.Runtime`, pure C#):** `TurnBasedWorld` / `ITurnBasedWorld`, `Battle` (`Perform(BattleAction)`:
  Attack, Ability, Defend, Item, Flee; `RefusalOf`), `BattleMenu` (команди поточного бійця з цілями або причиною, чому
  закрито), `IBattleAi` / `SimpleBattleAi`, `IBattleFormula` (сід; критичні удари), стихії, квести, навички партії,
  репутація, досягнення, `TurnBasedSaves`, `TurnBasedComposition.Configure`.
- **Unity (`TurnBased.Unity`, з v0.19):** `TurnBasedHost` (гра заповнює `ConfigureGame`; вороги ходять по черзі, забіг
  зберігається після кожного бою), `BattleScreen` (на панелях фреймворку), `PartyRows` (сторінки між боями).

### 7.3 Turns — покрокова сесія по мережі (з v0.14)

`TurnHost` (місце 0: лобі, `Start(rules, options)`, таймер ходу, повернення гравця за `RejoinToken`, вікна реакцій
`OpenPrompt`), `TurnClient`, спільний `ITurnSession`. Правила гри (`ITurnRules`: `CurrentSeat`, `CanAct`, `Apply`,
`ViewFor(seat)`, `DefaultIntent`) працюють лише на хості; кожне місце отримує свій вид (приховані карти лишаються
прихованими); `Notify(…, TurnVisibility.All/Seat/Host/Others)`. Тексти — `LocalizedText` (з v0.19). Протокол
`TurnProtocol` версії 2, id повідомлень 1300–1311, поверх будь-якого `INetworkTransport`. Гра: The Bloodline Hunt.

---

## 8. Мережа і кооп

| Частина | Стан |
|---|---|
| Контракти `INetworkTransport`, `INetworkManager`, `INetworkSerializer`, `INetworkPayload`, `NetworkAuthority` | [є], pure C# |
| `UnityNetworkTransport` (Unity Transport 2.5.3+, reliable-ordered + unreliable, фрагментація до 64 КБ, Relay) | [є], тести по реальному UDP |
| `LoopbackNetwork` / `LoopbackTransport` (у пам'яті, `GoSilent()` для тестів розриву) | [є] |
| Сесії: `LanSessionProvider` (IP; `LocalAddresses()` — домашня мережа, далі Tailscale / Radmin / ZeroTier), `LoopbackSessionProvider`, `UnityRelaySessionProvider` (код кімнати; лише з `com.unity.services.multiplayer` 2.4.1+) | LAN [є]; Relay [частково]: **між двома реальними мережами не перевірено** |
| Пошук ігор у локальній мережі: `LanDiscovery`, `LanDiscoveryResponder` (UDP 47777), `ActionRpgHost.SearchLanGames` | [є] з v0.16 |
| Кооп ActionRpg (`CoopHost`, `CoopClient`, `RemoteWorld`): хост авторитетний, 2–8 гравців, відбиток білду і контенту, перевірка швидкості, перепідключення (місце тримається 2 хв), таймаут 10 с; **протокол версії 16** | [є], EditMode (loopback) + PlayMode (UDP) |
| **Лобі перед грою** (`CoopHost.UseLobby`: вибір героя, готовність, `Kick`, `StartMatch(countdown)`) | [є] з v0.16 |
| Власні повідомлення гри: `CoopGameMessages.Add<T>(id ≥ 1200, authority)`; `SendLatestToAll` / `OnLatest<T>` — стан, що змінюється постійно | [є] з v0.5.0 / v0.6 |
| Захист на машині власника (`DefenseAuthority = Owner`), власний прогрес гостя (`PerPlayer`, `ProfileValidator`), прискорення, точки відродження, зміна сцени (`ChangeScene`) | [є] з v0.5.0–v0.6 |
| Що синхронізує кіт: позиції, влучання (критичні, множники), числа, ефекти, кулдауни (і скинуті), прогрес, гроші, спорядження, місця взаємодії, пауза, журнал як тексти для перекладу | [є] |
| Сесія ходів `RPGFramework.Turns` (розд. 7.3) | [є] з v0.14 |
| `ReplicationSystem`, `NetworkEntity`, `NetworkTransform`, `DeltaCompression` (загальна реплікація сутностей) | [частково]: старіша, **кіти її не використовують** |
| Передбачення на клієнті, міграція хоста, онлайн-матчмейкінг, Steam і EOS провайдери | [немає] |

Обмеження, які мусить врахувати GDD (networking.md, «Limits»):
- позицію свого персонажа кожен гравець надсилає сам (хост перевіряє швидкість і радіус атаки) — нормально для гри з
  друзями, не для змагальної;
- без передбачення влучання гостя видно через один round-trip;
- з `PerPlayer` хост довіряє прогресу, який приніс друг (або перевіряє його `ProfileValidator`);
- хост вийшов — гра закінчується для всіх;
- при зміні сцени розставляє гравців і прибирає зайвих ворогів гра (`OnSceneChanged`); сцени мають бути в build settings.

Як писати правила, сумісні з коопом (рецепт 11): `ConfigureGameModules` виконується **лише на хості**, `ConfigureSettings`
— на кожній машині; правило — модуль, що підписується на події `ActorWorld` і діє через нього; view читають `IActorWorld`
(`host.CurrentWorld`); власний стан гри хост надсилає своїми повідомленнями, дії друга приходять хосту як
Client-повідомлення (id від 1200).

---

## 9. Гра на фреймворку

### 9.1 Форма проєкту гри (створює Game Kit)

```
Assets/<Game>/
  Scripts/Core/   <Game>.Core (noEngineReferences): правила, стан, події, модулі, системи
  Content/        Game Setup, Characters, предмети, лут, квести, тексти (ScriptableObject)
  Scripts/        <Game>: хост (ActionRpgHost, TurnBasedHost або GameHostBehaviour), view, input, HUD
  Editor/         <Game>.Editor
  Tests/          <Game>.Tests: EditMode + <Game>ArchitectureTests
  PlayTests/      PlayMode-тести сцени
CLAUDE.md, Docs/RPGFramework/ (architecture.md, recipes 01–17), GAME.md, FRAMEWORK-GAPS.md, DEVLOG.md, CI workflow,
.gitattributes (Git LFS)
```

New Game також виставляє Player Settings (Run In Background, Linear, назва компанії). Конвеєр рендерингу лишає
проєктовим (розд. 1).

### 9.2 Архітектурні правила гри (тести `<Game>ArchitectureTests`)

| Правило | Суть |
|---|---|
| GA01 | увесь код гри в `.asmdef` |
| GA02 | є збірка `<Game>.Core` |
| GA03 | `<Game>.Core` без UnityEngine |
| GA04 | правила не залежать від view, editor, tests |
| GA05 | без `FindObjectOfType`, `GameObject.Find`, `SendMessage` |
| GA06 | без синглтонів |
| GA07 | події — `readonly struct : IEvent` з get-only властивостями |
| GA08 | `UnityEditor` лише в editor-коді |
| GA09 | MonoBehaviour / ScriptableObject — у файлі свого імені |
| GA10 | кожна збірка гри має тести |
| GA11 | гра не оголошує типів з іменами типів фреймворку і не пише в `RPGFramework.*` |
| GA12 | кожен `// framework-gap: <id>` має розділ у `FRAMEWORK-GAPS.md` |
| GA13 | гра використовує core-сервіси (розд. 4; з v0.12 і звук); виняток — рядок з `// framework-ok: <причина>` |
| GA14 | правила гри використовують ігрові модулі: без ручного зваженого вибору, грошей, кулдаунів, таймерів, інвентарю списком id (з v0.6, розширено у v0.8.1); виняток — лише маркер `// framework-gap: <id>` |

### 9.3 Коли фреймворку чогось бракує

Не відбудовувати модуль у грі, не урізати функцію, не підробляти. Записати розділ у `FRAMEWORK-GAPS.md` (потреба,
чого немає, бажаний API, де замінник), зробити найменший замінник за інтерфейсом у `<Game>.Core` з маркером
`// framework-gap: <id>` і тестами. Велика прогалина (ціла система) — спершу рішення, чи додавати її у фреймворк.
Після оновлення фреймворку кожну відкриту прогалину звіряють із CHANGELOG і замінюють замінник.

---

## 10. Правила коду фреймворку і як вони перевіряються

| Правило | Як забезпечено |
|---|---|
| Фреймворк не посилається на гру | правило README; `.asmdef` пакета не мають посилань на ігри |
| Без синглтонів, `FindObjectOfType`, `UnityEditor` у runtime | правила README; для **гри** — GA05, GA06, GA08 |
| Оголошена подія публікується | аудит v0.3.0 + тест на кожну подію |
| Компоненти ECS — дані-структури | `ECS/Tests/ArchitectureTests` |
| Шари (L0 не бачить L2 тощо), граф п. 5.3, відсутність дублікатів імен | **тестів у пакеті немає**; дотримано за фактом `.asmdef`, дублікати є (розд. 12) |
| `.meta` з валідним GUID для кожного файлу | CI-крок «Unity .meta integrity» |
| Тексти на екрані — ключі, не рядки | тексти кітів через `ILocalization`; `LocalizationChecks` для таблиць гри |

Тестова база: v0.22.0 (коміт `414581a`, код тегу) — EditMode **2005/2005**, PlayMode **17/17**; CI run `41226271688`.

---

## 11. Прийняті рішення

| # | Питання | Рішення |
|---|---|---|
| 9.1 | Async-модель | **UniTask** 2.5.10 |
| 9.2 | Addressables | **опційна збірка** `AssetManagement.Addressables`, define `RPG_ADDRESSABLES` при встановленому пакеті |
| 9.3 | Experimental-модулі | лишились у пакеті; зрілість — за використанням у кітах (розд. 5.1) |
| 9.4 | Поширення | **UPM-пакет із git-тегом**; гра — окремий проєкт; ліцензія MIT |
| — | Мережева модель | **хост авторитетний**, власні протоколи кітів (кооп ActionRpg, Turns) поверх `INetworkTransport`; Unity Transport; NGO / FishNet / Mirror не використовуються |
| — | Вхід | Input System (залежність пакета) |
| — | Другий жанр | TurnBased: правила + готовий хост і екран бою (з v0.19) |
| — | Тексти | ключі й `LocalizedText`; правила не форматують рядків для гравця (з v0.17) |
| — | Конвеєр рендерингу | не нав'язується; пакет працює в Built-in і URP |
| — | Звідки беруться зміни | з прогалин реальних ігор (`FRAMEWORK-GAPS.md`: MythRunner FG-*, The Bloodline Hunt BH-*, демо в `D:\RPG-Games`) за п'ятьма фільтрами `CLAUDE.md` |

---

## 12. Відомі розбіжності і технічний борг

1. **Namespace із суфіксом `.Runtime`** в Inventory, Guilds, Companions, DynamicEvents, Environment, частині
   Achievements, Housing; `RPGFramework.Core.Composition` замість `RPGFramework.Core`.
2. **`noEngineReferences` не виставлено** в низці збірок без Unity-коду за змістом (Combat, Progression, Quests,
   Inventory, Factions, Abilities, Events, DI, Core, Systems…). Для гри це неважливо (її `Core` перевіряє GA03).
3. **Дублікати імен типів між модулями:** `RewardType` (Achievements і Quests), `ConditionType` (Dialogue і Trading).
4. **Дві моделі реплікації:** протоколи кітів (робочі) і стара `ReplicationSystem` / `DeltaCompression`.
5. **Шарові архітектурні тести самого фреймворку в пакеті відсутні.**
6. `UI.Runtime` залежить від шести L2-модулів (Items, Inventory, Quests, Loot, Stats, Characters) — панелі під RPG.
7. Unity Relay не перевірений між реальними мережами.
8. Частина тексту TurnBased (журнал бою, підписи `BattleMenu`) ще англійська; відмови вже ключі (v0.20).

---

## 13. Чекліст для автора GDD

Для кожної функції гри в GDD варто відповісти:

1. **Жанровий старт:** ActionRpg (реальний час, 3D, кооп з коробки), TurnBased (хост і екран бою), Turns (покрокова
   гра по мережі), чи власний хост на `GameHostBehaviour` і модулях?
2. **Які модулі** (розд. 5.1) і чи вони вже стоять під кітом або грою (зрілість). Модуль без користувача — закласти час.
3. **Що лишається грі:** правила, яких модулі не виражають (розд. 5.2), контент як ассети, view, арт і конвеєр рендерингу.
4. **Кооп:** чи вкладається в модель розд. 8. Якщо ні — це **framework gap**, а не код у грі.
5. **Дані дизайнера:** що налаштовується в інспекторі (Game Setup, Character, Item, Loot Table, Quest, Audio Library,
   Localization Library, Render Budget та ін.).
6. **Мови:** усі тексти гравцю — ключі в таблицях; правила дають `LocalizedText` або коди відмов.
7. **Тести:** EditMode з `GameHost` і композицією гри; кооп — по `LoopbackNetwork`; сцена і бюджет рендерингу — PlayMode
   (`RenderSection.Measure`).
8. **Прогалини:** список того, чого бракує (розд. 5.2, 8 «немає»), з позначкою «framework gap» і бажаним API.

Не потрібно планувати у GDD (вже є): бутстрап і цикл, DI, шину подій, системи і фази, паузу, таймери, кубики, пули,
ассети, сцени, UI-навігацію й панелі з коду, звук, локалізацію, налаштування гравця, діагностику з бюджетом рендерингу,
збереження з версіями, транспорт UDP, LAN-сесії й пошук ігор, кооп-протокол з лобі, сесію ходів, PvP-режими, генератори
і архітектурні тести гри.
