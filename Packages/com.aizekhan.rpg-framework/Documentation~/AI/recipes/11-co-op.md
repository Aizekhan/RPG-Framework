# Recipe: co-op (2-8 players, local network or internet)

Every game built on `ActionRpgHost` can already be played together; there is nothing to switch on.

| Task | Do |
|---|---|
| Play together | One player presses **F2 > Host a game**; the panel shows the address friends type. The others press **F2**, type that address and **Join**. Up to 4 players by default; **Max Players** on `ActionRpgHost` allows up to 8. |
| From the command line | `MyGame.exe -host` and `MyGame.exe -join 192.168.1.20` (`-port 7790` for another port). Two builds on one PC: `-host` in one, `-join 127.0.0.1` in the other. |
| Names and colours | Each player types a name in F2 (remembered). It shows above their head with a ring in their party colour: host inspector > **Party Colors**, by seat (host first, then friends in the order they joined). |
| Port, or no co-op at all | Host inspector > **Co-op**: untick it to hide F2, or change **Coop Port** (7777). Players allow it through the firewall the first time. |
| Code | `host.HostGame()`, `host.JoinGame(code)`, `host.LeaveGame()`; `host.Role`, `host.JoinCode` (and `host.OtherJoinCodes`: the host's VPN addresses such as Tailscale or Radmin), `host.CoopStatus` (why a join failed, or "The host left the game."). |
| Over the internet (Unity Relay) | Install **Multiplayer Services** (`com.unity.services.multiplayer` 2.4.1+) in Package Manager; nothing else in code. F2 then offers **Local network / Internet (Unity Relay)**: the host gets a short code, friends type it. The project must be linked to a Unity Gaming Services project: **Edit > Project Settings > Services** (anonymous sign-in on, the default); until then F2 says so. Check Unity's current Relay pricing and free tier before shipping. Command line: `-provider unity-relay -host`, `-provider unity-relay -join ABC123`. |
| Your own messages (state only your game has, a friend's actions) | Override `ConfigureCoopMessages(messages)`: `messages.Add<WeaponDroppedMessage>(1200, MessageAuthority.Server)`, `messages.Add<FireMessage>(1201, MessageAuthority.Client)` (a class `: NetworkMessage, INetworkPayload` whose `MessageTypeId` is that id; ids from `CoopProtocol.FirstGameMessageId`). The host handles and sends them in `OnCoopHostOpened(host)`: `host.On<FireMessage>((playerId, fire) => ...)`, `host.SendToAll(msg)`, `host.Send(playerId, msg)`; a joined machine in `OnCoopClientCreated(client)`: `client.On<WeaponDroppedMessage>(msg => ...)`, `client.Send(msg)`. A message the other side may not send is refused (a client sending a host-only one throws). |
| Progress of each player's own | Game Setup > **Co-op progress > Progress Mode = Per Player** (code: `ActionRpgSettings.ProgressMode`): level, experience, backpack, quests and kills per player (`World.LevelOf(id)`, `InventoryOf(id)`, `GrantExperience(id, xp)`, `GiveItem(id, item, qty)`). A friend brings their saved progress and takes home what they earned (with **Save Progress** on, automatic). `LootToBackpack = false`: drops are only announced (`LootDroppedEvent`), whoever picks one up gets it via `GiveItem`. `Shared` (the default): one party progress. |
| Rules the asset does not show | Override `ConfigureSettings(settings, content)` on your host: `settings.DamageCalculator`, `content.StatusEffects`, `content.Abilities`. It runs on every machine, so host and friends agree. |
| Hub and runs in different scenes | `await host.ChangeScene("Run")` (alone or hosting): the game, party and link stay, friends load the scene too, late joiners as well. Put players at the new scene's spawn points in `OnSceneChanged(sceneName)` with `TeleportPlayer(id, pose)`. The new scene holds the level; an `ActionRpgHost` in it steps aside. Scenes must be in the build settings. |
| Dodge or block decided by the friend | `host.DefenseAuthority = DefenseAuthority.Owner` (in `OnCoopHostOpened`): attacks on a friend wait for their machine's answer: subscribe to `IncomingAttackEvent` on the client bus and answer `client.Defend(attackId, DefenseOutcome.Dodged)` (or `Blocked` with a damage share) within `host.DefenseWindow` (0.3 s); no answer lands. `host.AnnounceAttack(a, t, windUp)` for telegraphed hits. |
| Speed boosts, respawn points | `host.SetAllowedSpeedMultiplier(playerId, 1.5f, seconds)` before a dash or boost (haste effects count by themselves). `host.RespawnPoints = new MyCheckpoints()` (`IRespawnPointProvider`) decides where a reconnecting or revived player appears. |
| A friend who reconnects | `host.PlayerRejoined += (before, now) => ...` (in `OnCoopHostOpened`): a lost player back within `host.RejoinWindow` (2 min) gets a new player id; move what your game keeps under the old one (their runner, their loot) to the new one. `PlayerJoined` follows. |
| Check what a friend brings | `host.ProfileValidator = new MyCheck()` (`IGuestProfileValidator`): return `GuestProfileCheck.Accept(profile)` (as it is or cleaned), `Fresh()` or `Refuse("reason")`. With Per Player progress a friend's own profile is taken once per session; coming back, they continue with what they left with. |
| State sent many times a second | `host.SendLatestToAll(snapshot)` (unreliable; `final: true` for the last one, reliable) and `client.OnLatest<Snapshot>(s => ...)`, which drops late packets. Not `SendToAll` 10 times a second: reliable messages queue up behind a lost one. |
| Money and shops | Gold drops from loot tables and quest rewards into each player's wallet (or the party's); `World.CurrencyOf()`, and a purchase is `World.TakeOffer(offerId)`: on a friend's machine it goes to the host (recipe 16). |
| Another way to connect | Set `host.SessionProvider` (an `INetworkSessionProvider`: platform lobbies, your own server) before hosting or joining, or register one in `NetworkSessionProviders` from its own module so every game offers it. The default is `LanSessionProvider`. |

How it works, so new rules stay co-op ready:

- **The host runs the game.** Its `ActorWorld` has every player in one party (`World.Players`): shared level,
  experience, backpack and quests (or each player's own with `ProgressMode.PerPlayer`); a kill by anyone counts for
  the party (with `PerPlayer`, for the killer). Joined machines run no rules: they show the
  host's world through `IActorWorld` (`host.CurrentWorld`), and their attacks and revives go to the host to decide.
- **Your `ConfigureGameModules` runs only on the host** (and when playing alone). A rule written as a module that
  subscribes to `ActorKilledEvent` / `ActorDamagedEvent` and acts through `ActorWorld` therefore works in co-op
  unchanged. Use `ActorWorld.PlayerId` for "the party", `CreditOf(entityId)` for who a deed counts for.
- **Views use `IActorWorld`**, never `ActorWorld`, so they work on both sides. On a joined machine `World` is null;
  read `CurrentWorld`.
- **Something only your game knows** (weapons on the floor, a timer, spawners, a downed state) does not reach the
  others on its own. Characters, positions, hits, deaths, health, status effects and cooldowns are sent already;
  anything else is a message of your own (table above): the host sends the state, friends send only their actions
  and the host decides. Logic that must run on a joined machine (showing that state) goes in `OnCoopClientCreated`.
- The host's and the friends' builds must match: the same characters, items and quests (`CoopProtocol.Fingerprint`).
  A different build is refused with "The host plays a different version of the game".
- **The host checks movement**: a friend may not move faster than their character's Move Speed; if they do, the host
  puts them back (also on their machine). Move a player on purpose with `CoopHost.Teleport`. Players fighting each
  other: see `13-pvp.md`.
- A lost link (10 s of silence) is handled for you: the host keeps the player's seat for 2 minutes and their game
  reconnects by itself; F2 shows "unstable" after 2 s. Tune with `CoopHost.RejoinWindow` and
  `ActionRpgHost.ConnectionTimeout`; read `CoopClient.EndKind` to tell a lost link from a refusal.

Tests: `CoopTests` in the package play a host and friends over `LoopbackNetwork` without sockets; copy that pattern
for your own co-op rules. `CoopPlayTests` play two hosts over real UDP.
