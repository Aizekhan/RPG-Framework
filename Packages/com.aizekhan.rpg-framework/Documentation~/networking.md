# Networking: where it stands and the proposed path

Status on 2026-10-26. **No game made with this package can be played over a network yet.**

## What exists

| Part | State |
|---|---|
| Contracts: `INetworkTransport`, `INetworkManager`, `INetworkConnection`, `INetworkEntity`, `IReplicationSystem`, `INetworkSerializer` | done, engine-free |
| `NetworkManager`, `NetworkConnection`, `ReplicationSystem`, `NetworkSerializer`, message types (spawn, despawn, state, delta, RPC, transform, ping) | done, tested against a mock transport (35 tests) |
| `NetworkAuthority` (which side may send which message), several handlers per message | done, tested |
| `DeltaCompression` (`DeltaSerializer`) | done, no tests |
| A real transport (sockets / Unity Transport / Steam / relay) | **missing** |
| Replication of the action RPG (`ActorWorld`) | **missing** |
| Lobby, matchmaking, NAT traversal, host migration, client prediction | **missing** |

The pieces are a protocol without a wire: messages can be built, serialized, authorised and dispatched, but only
`MockNetworkTransport` (in the tests) moves them.

## Proposed path (co-op action RPG, 2-4 players)

1. **Host-authoritative model.** The host runs `ActorWorld` as today; clients only show it and send input.
   `ActorWorld` is engine-free and already announces everything on the bus (`ActorSpawnedEvent`,
   `ActorDamagedEvent`, `ActorKilledEvent`, quests, loot), so replication is "forward these events to clients" plus
   periodic transforms. No rule has to be rewritten.
2. **First transport: Unity Transport (`com.unity.transport`)** behind `INetworkTransport`, in an optional assembly
   compiled only when the package is installed (as the Addressables provider is). It is Unity's own low-level UDP
   layer with reliable pipelines and Relay support, and it does not impose an object model that would compete with
   `ActorWorld`. Netcode for GameObjects, Mirror or FishNet each bring their own spawning and ownership model; an
   adapter to one of them is possible later through the same interface.
3. **ActionRpg networking module:** host broadcasts actor events and transforms; clients send move/attack intents
   that the host validates (`NetworkAuthority` already rejects client-sent state). Each player gets an actor; the
   wave and quest rules stay on the host.
4. **Proof:** two Editor instances (or ParrelSync / Multiplayer Play Mode) play the same valley; PlayMode tests run a
   host and a client over the loopback transport.

Rough size: transport adapter 1-2 days, replication module and multi-player `ActorWorld` 4-6 days, tests and a
sample scene 2 days.

## Decision needed

Multiplayer is not started. Before any of this: is co-op in scope for the games you plan, and which transport
should come first (Unity Transport proposed above)?
