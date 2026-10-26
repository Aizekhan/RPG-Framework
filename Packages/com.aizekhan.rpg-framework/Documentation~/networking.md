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

## Connecting players: session providers

Transport (how bytes travel) and sessions (how a game is opened, found and joined) are separate. The game asks an
`INetworkSessionProvider` for a session and gets a ready `INetworkTransport` back; `CoopHost` / `CoopClient`, the
messages and the rules never see how players connected.

```
INetworkSessionProvider            HostAsync(options) -> HostedSession { Transport (listening), JoinCode }
                                   JoinAsync(joinCode) -> Transport (connecting)
  LanSessionProvider               UDP on the local network / a VPN; the code is "IP" or "IP:port"     (done)
  LoopbackSessionProvider          in memory, for tests                                                 (done)
  Unity Relay provider             room code from Unity Relay, traffic through Unity's relays           (proposed)
  Steam / EOS providers            lobbies, friend invites, the platform's relays                       (later)
NetworkManager.StartServer() / StartClient()   take over a transport the provider already opened
ActionRpgHost.SessionProvider                  LAN by default; set another before hosting or joining
```

Audit of the co-op code before this change (8948d16): the protocol, per-player diffs, authority, fingerprint,
`RemoteWorld` and the party rules were already independent of the connection. Three places assumed an IP address:
`CoopHost.Open(port)` / `CoopClient.Join(address, port)`, `INetworkTransport.StartServer(port)` /
`Connect(address, port)`, and `ActionRpgHost`, which built the UDP transport itself and showed the LAN address. The
first two keep their address overloads (tests, simple tools) and gained session overloads; the host now goes only
through the provider. A provider for a service lives in an assembly of its own that references the service's SDK, so
the framework never depends on one.

## Playing over the internet: options compared (researched 2026-10-26)

| Option | Cost | What players do | Needs | Size of the work here |
|---|---|---|---|---|
| LAN / VPN (done) | free | type the host's IP; between homes install Tailscale or ZeroTier | nothing | done |
| **Unity Relay** | free up to an average of 50 concurrent players a month and 3 GiB per player (150 GiB max), then $0.16 per average CCU and $0.09/GiB (US/EU); figures Unity published in 2023, confirm before launch | host shares a short join code | a Unity Gaming Services project linked to the Unity project; packages `com.unity.services.relay` (+ authentication, anonymous sign-in) | small: the transport already is Unity Transport, Relay plugs into its settings. About 1-2 days with tests |
| Steam (Steam Networking Sockets + lobbies) | free relays (Valve's network, hides IPs); $100 Steam Direct fee per game | invite from the Steam friends list, or join a lobby | the game on Steam (app id), Steamworks.NET or Facepunch.Steamworks; players need Steam | medium: a new transport over Steam sockets plus a lobby provider. About 3-5 days |
| Epic Online Services (P2P + lobbies) | free (no fees for P2P, lobbies, accounts) | invite or lobby; works on any store | an Epic developer account, the PlayEveryWare EOS plugin, a login per player (device id is enough) | medium-large: new transport over EOS P2P, big plugin. About 4-6 days |
| Photon (Realtime / Fusion) | 20 CCU free for development only; 100 CCU free only for Fusion/Quantum games; paid plans above | room name or code | Photon account and SDK; its own networking model | poor fit: Photon wants to be the whole netcode, competing with `ActorWorld` replication |
| Own relay + lobby server | a small VPS (a few dollars a month) plus our time | room code | a server we run, update and keep secure | large: 1-2 weeks, then operations |

Recommendation: **Unity Relay next**, as `RPGFramework.Networking.UnityRelay` (compiled only when the Relay package is
installed, like the Addressables provider). It is the smallest step, store-independent and free at a co-op game's
scale. Steam and EOS stay possible later as providers of their own, for a game that ships there; nothing in the
co-op changes for them. To start it, the project needs a Unity Gaming Services project (Edit > Project Settings >
Services), which is the owner's decision and account.

Sources: [Unity Gaming Services pricing](https://unity.com/products/gaming-services/pricing),
[How is the Relay service priced](https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced),
[Steam networking](https://partner.steamgames.com/doc/features/multiplayer/networking),
[EOS plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity/blob/stable/com.playeveryware.eos/README.md),
[Photon pricing](https://doc.photonengine.com/photon/current/pricing).

## Limits worth knowing

- Positions are trusted from each player for their own character (the host checks reach for attacks). Fine among
  friends; a competitive game would need server-side movement.
- No client-side prediction: a joined player's hits show after one round trip (a few milliseconds on a LAN).
- Progress is saved on the host only; a friend who joins brings nothing and takes nothing home.

## Next, if wanted

- A Unity Relay session provider for playing over the internet without a VPN (see the comparison above).
- Per-player backpacks or loot rolls, if a game wants them instead of the shared party.
