# Networking: where it stands

Status on 2026-10-26. **Action RPGs made with this package can be played in co-op by 2-4 players on one network**
(LAN, or a VPN such as Tailscale or ZeroTier between homes).

## What exists

| Part | State |
|---|---|
| Contracts: `INetworkTransport`, `INetworkManager` (with `PeerConnected` / `PeerDisconnected`), `INetworkConnection`, `INetworkSerializer` | done, engine-free |
| `NetworkManager`, `NetworkSerializer`, game messages with their own fields (`INetworkPayload`), `NetworkAuthority` (which side may send what) | done, tested |
| `UnityNetworkTransport`: UDP over Unity Transport, reliable-ordered and unreliable pipelines, fragmentation up to 64 KB | done, tested over real UDP |
| `LoopbackNetwork`: in-memory transport for tests and in-process play | done, tested |
| Action RPG co-op (`ActionRpg/Runtime/Coop`): `CoopHost`, `CoopClient`, `RemoteWorld`, the party in `ActorWorld` | done, tested over loopback (EditMode) and UDP (PlayMode) |
| `ActionRpgHost` co-op: F2 panel, `-host` / `-join`, smooth movement of others (`RemoteMotion`) | done, PlayMode test with two hosts |
| `ReplicationSystem`, `NetworkEntity`, `NetworkTransform`, `DeltaCompression` (generic entity replication) | older, tested on a mock transport; the action RPG does not use them |
| Internet play without a VPN (Unity Relay, NAT punch-through), lobby / matchmaking, host migration, client-side prediction | **missing** |

## How the action RPG co-op works

1. **Host-authoritative.** The host runs `ActorWorld` and the game's own rules. Joining adds a player to the party
   (`ActorWorld.AddPlayer`); leaving removes them. The party shares level, experience, backpack and quests.
2. **Joined machines mirror.** `CoopClient` keeps a `RemoteWorld` (an `IActorWorld`) in step and publishes
   `ActorSpawnedEvent`, `ActorDespawnedEvent` and `ActorDamagedEvent` on its own bus, so the presenter, views and HUD
   are the same code as in a solo game. Attacks and revives are sent to the host (`CoopIntentMessage`), which checks
   them (alive, an enemy, within reach) and applies them through `ActorWorld`.
3. **What is sent.** Reliable, 20 times a second when something changed: characters entering and leaving, hits,
   changed numbers (health, stats, alive) and the party's progress, each measured against what that player last saw
   (a late joiner gets everything at once). Unreliable, 20 times a second: positions (host → everyone; each player →
   host, their own character only).
4. **Messages** (ids 1100-1104): `CoopHello` (game fingerprint), `CoopWelcome` (accepted with a player id, or the
   reason), `CoopWorld`, `CoopMotion`, `CoopIntent`. Hello and Intent are client-only, Welcome and World host-only
   (`NetworkAuthority`).
5. **Refusals in plain words:** a different build ("The host plays a different version of the game"), a full party,
   nobody at that address (after about 5 seconds), the host leaving. The joined player is then back in a game of
   their own.

## Limits worth knowing

- Positions are trusted from each player for their own character (the host checks reach for attacks). Fine among
  friends; a competitive game would need server-side movement.
- No client-side prediction: a joined player's hits show after one round trip (a few milliseconds on a LAN).
- Progress is saved on the host only; a friend who joins brings nothing and takes nothing home.

## Next, if wanted

- Unity Relay behind `INetworkTransport` for playing over the internet without a VPN (join code instead of an IP).
- Per-player backpacks or loot rolls, if a game wants them instead of the shared party.
