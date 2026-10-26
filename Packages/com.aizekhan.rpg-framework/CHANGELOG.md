# Changelog

## [Unreleased]

- `ActionRpgHost` **Max Players** (2-8, default 4) in the inspector and as a property; it sets the hosted session and `CoopHost.MaxPlayers`. Eight players are tested over loopback (EditMode) and over real UDP with eight games (PlayMode). `CoopProtocol.MaxSupportedPlayers`
- Party colours: seats past 'Party Colors' get generated colours, each the bright hue least like those before it, so no two players share one. `PlayerTagView.DefaultPalette()`
- Friends appear in a ring around the start instead of inside each other
- **Fix:** `JoinGame` called before the game had started (a host created in code, a join on launch) dropped back to solo with "You left the game.": the restart closed the new connection as if the player had left
- The co-op panel says how many can join instead of a fixed "up to three"
- `networking.md`: Unity Relay compatibility check and adaptation plan (nothing integrated yet)

## [0.2.0] - 2026-10-26

Action RPG from inspector assets, the Game Kit (New Game creates a playable game), co-op for 2-4 players with
session providers, and a real network transport. Install with `#v0.2.0`.

- `Networking`: session providers. `INetworkSessionProvider` opens a session (a listening transport and the code friends join with) or joins one by code, so how players connect is pluggable: `LanSessionProvider` (IP or IP:port over UDP), `LoopbackSessionProvider` (tests); `NetworkManager.StartServer()` / `StartClient()` take over a transport a provider opened. `ActionRpgHost.SessionProvider` (LAN by default), `JoinCode`; `HostGame()` / `JoinGame(code)` no longer take ports (`-port` on the command line, **Coop Port** in the inspector). Players see a plain-words reason for a bad code or a taken port
- `ActionRpg`: co-op players have names and colours. Each player's name (F2 > Your name, remembered; the computer's user name at first) shows above their head with a ring at their feet in their party colour (**Party Colors** on the host, by seat: host first, then friends as they joined; a seat is kept while its player stays and reused after they leave). Names are cleaned and made unique ("Sam 2"). The F2 panel and the top line list the party in colour. `Actor.PlayerName` / `Seat`, `ActorWorld.SetPlayerName`. Co-op protocol version 2: older builds are refused
- `ActionRpg`: co-op for 2-4 players on one network. Any `ActionRpgHost` game: F2 > **Host a game** / type an address and **Join** (or `-host` / `-join <ip>`). The host runs the rules; friends join its party (shared level, experience, backpack, quests) and see its world through `RemoteWorld`; their attacks and revives are decided by the host. Different builds and full parties are refused with a reason; a friend whose host leaves is back in a game of their own. `CoopHost` / `CoopClient` are engine-free and tested over `LoopbackNetwork`; a PlayMode test plays two hosts over UDP. Recipe 11
- `ActionRpg`: `ActorWorld` party (`AddPlayer`, `RemovePlayer`, `Players`, `CreditOf`); enemies chase the nearest living player; views and the HUD read `IActorWorld`; `ActionRpgHost.CurrentWorld`
- `Bootstrap`: `GameHostBehaviour.Restart()` starts a new game from `ConfigureModules`
- `Networking`: `UnityNetworkTransport` gives up on an address nobody answers at after about 5 seconds; `NetworkManager` no longer throws on a bad message when it has no logger
- `Networking`: a real transport, `UnityNetworkTransport` over Unity Transport (UDP; reliable-ordered and unreliable pipelines with fragmentation); `LoopbackNetwork` / `LoopbackTransport` for tests and in-process play; game messages with their own fields (`INetworkPayload`); `PeerConnected` / `PeerDisconnected` events on `INetworkManager`. The package now depends on `com.unity.transport`
- `ActionRpg`: the backpack shows each item with its icon (new **Icon** sprite on Item assets; initials in a rarity-coloured frame when there is none). `ActionRpgHost` **Diagnostics** (on by default, Editor and development builds only): F3 shows systems, events and frame times
- `Diagnostics`: `DiagnosticsOverlay`'s toggle key works with the Input System (it needed the legacy input manager before)
- `ActionRpg`: `HealthBarView` tints a mesh fill (not only a UI image) with the player or enemy color
- Recipes point at the package's `ActorPresenter`, `PlayerController` and `WaveSystem` instead of Goblin Valley classes
- `ActionRpg`: the player's progress is saved between sessions (level, experience, kills, backpack, quest progress, finished quests; rewards are not paid twice on load). `ActionRpgSaves` over the framework's `SaveGameCoordinator`; `ActionRpgHost` **Save Progress** (on in new games) saves on level-up, finished quests and quit, with a **Delete Saved Progress** context menu
- `Persistence`: writing a save recreates the save folder if it was deleted while the game ran
- `ActionRpg`: character and backpack panel in the HUD (I / Tab / gamepad Select); `ActorKilledEvent`, public `ActorWorld.GiveItem`, `Announce` and `ExperienceForNextLevel` for game rules
- Editor menus are all under **RPG Framework** (the tool windows moved from Tools > RPG Framework to RPG Framework > Tools)
- Install: the package depends on `com.unity.test-framework` and `com.unity.inputsystem`, and `GameKit.TestSupport` compiles without the package being testable, so a new game's tests build in a fresh project (verified: empty Unity 6 project + UniTask + this package, New Game, 14/14 tests, Play)
- `GameKit`: New Game creates a playable action RPG: starter content (hero, goblin, items, loot table, quest, Game Setup), a scene with ground and a host linked to the setup, and tests that boot the real setup and play a wave. AI guidance is now optional (checkbox / `-aiGuidance`). Recipe 10: content in the inspector
- `ActionRpg`: a playable action RPG assembled in the inspector. `Character` and `Game Setup` assets (player, enemies, waves, items, loot, quests) with validation in plain words; `ActionRpgHost` runs it with any prefab (player controls, enemy AI, strike lunge and health bar are added when missing; no prefab shows a placeholder capsule), camera follow and HUD. Engine-free rules in `ActorWorld` / `ActionRpgModule`. Requires `com.unity.inputsystem`
- `GameKit`: New Game / New Feature generators (menu and batch mode) that create the layered structure, scene, composition and tests; game architecture rules GA01-GA10 as a test base class (`GameArchitectureTests`); AI guidance installer (`CLAUDE.md` block, architecture guide, recipes, CI workflow)
- `Presentation`: `EntityView` and `EntityViewRegistry` link entities to the GameObjects that show them (pooled views can be rebound)

## [0.1.0] - 2026-10-26

First packaged release. The framework moved from `Assets/_Framework` into the embedded package
`com.aizekhan.rpg-framework`; asset GUIDs are unchanged.

- Composition: `GameHost`, `GameHostBehaviour`, ready-made gameplay modules (`Bootstrap.Gameplay`)
- Event bus integration across Combat, Progression, Quests, Inventory, Guilds and Abilities
- Asset management with Resources and optional Addressables providers; `PrefabSpawner` with pooling
- UI navigation (screens, modals, transitions); RPG panels built on it
- Persistence with checksummed save slots and metadata sidecars
