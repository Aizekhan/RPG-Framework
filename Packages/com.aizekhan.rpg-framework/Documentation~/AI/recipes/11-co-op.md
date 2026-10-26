# Recipe: co-op (2-4 players on one network)

Every game built on `ActionRpgHost` can already be played together; there is nothing to switch on.

| Task | Do |
|---|---|
| Play together | One player presses **F2 > Host a game**; the panel shows the address friends type. The others press **F2**, type that address and **Join**. Up to 4 players. |
| From the command line | `MyGame.exe -host` (optional port after it) and `MyGame.exe -join 192.168.1.20`. Two builds on one PC: `-host` in one, `-join 127.0.0.1` in the other. |
| Port, or no co-op at all | Host inspector > **Co-op**: untick it to hide F2, or change **Coop Port** (7777). Players allow it through the firewall the first time. |
| Code | `host.HostGame()`, `host.JoinGame(address)`, `host.LeaveGame()`; `host.Role`, `host.CoopStatus` (why a join failed, or "The host left the game."). |

How it works, so new rules stay co-op ready:

- **The host runs the game.** Its `ActorWorld` has every player in one party (`World.Players`): shared level,
  experience, backpack and quests; a kill by anyone counts for the party. Joined machines run no rules: they show the
  host's world through `IActorWorld` (`host.CurrentWorld`), and their attacks and revives go to the host to decide.
- **Your `ConfigureGameModules` runs only on the host** (and when playing alone). A rule written as a module that
  subscribes to `ActorKilledEvent` / `ActorDamagedEvent` and acts through `ActorWorld` therefore works in co-op
  unchanged. Use `ActorWorld.PlayerId` for "the party", `CreditOf(entityId)` for who a deed counts for.
- **Views use `IActorWorld`**, never `ActorWorld`, so they work on both sides. On a joined machine `World` is null;
  read `CurrentWorld`.
- **Something only a view can know** (a position, an animation) does not reach the others on its own. Characters'
  positions are sent already (the presenter is the `IActorMotion`); anything else needs a message of your own
  (`INetworkPayload`, ids 1200 and up) on `NetworkManager`.
- The host's and the friends' builds must match: the same characters, items and quests (`CoopProtocol.Fingerprint`).
  A different build is refused with "The host plays a different version of the game".

Tests: `CoopTests` in the package play a host and friends over `LoopbackNetwork` without sockets; copy that pattern
for your own co-op rules. `CoopPlayTests` play two hosts over real UDP.
