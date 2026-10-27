# Networking: where it stands

Status on 2026-10-26. **Action RPGs made with this package can be played in co-op by 2-8 players on one network** (4 by default, `Max Players` on `ActionRpgHost`; 8 tested over UDP)
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
| Internet play without a VPN: `RPGFramework.Networking.UnityRelay` (compiled only with the Multiplayer Services SDK) | **built and tested against a fake room service; not yet proven between two real networks** |
| Lobby browser / matchmaking, host migration, client-side prediction | missing |

## How the action RPG co-op works

1. **Host-authoritative.** The host runs `ActorWorld` and the game's own rules. Joining adds a player to the party
   (`ActorWorld.AddPlayer`); leaving removes them. The party shares level, experience, backpack and quests, or with
   `ActionRpgSettings.ProgressMode = PerPlayer` each player has their own, brought from their save (`CoopClient.Profile`)
   and taken home (`RemoteWorld.CaptureProgress`).
2. **Joined machines mirror.** `CoopClient` keeps a `RemoteWorld` (an `IActorWorld`) in step and publishes
   `ActorSpawnedEvent`, `ActorDespawnedEvent` and `ActorDamagedEvent` on its own bus, so the presenter, views and HUD
   are the same code as in a solo game. Attacks and revives are sent to the host (`CoopIntentMessage`), which checks
   them (alive, an enemy, within reach) and applies them through `ActorWorld`.
3. **What is sent.** Reliable, 20 times a second when something changed: characters entering and leaving, hits,
   changed numbers (health, stats, alive) and the party's progress, each measured against what that player last saw
   (a late joiner gets everything at once). Unreliable, 20 times a second: positions (host → everyone; each player →
   host, their own character only).
4. **Messages** (ids 1100-1108): `CoopHello` (game fingerprint, brought progress), `CoopWelcome` (accepted with a
   player id, or the reason), `CoopWorld`, `CoopMotion`, `CoopIntent`, `CoopIncomingAttack` / `CoopDefenseResult`
   (defence decided on the owner's machine, `CoopHost.DefenseAuthority = Owner`). Hello and Intent are client-only,
   Welcome and World host-only (`NetworkAuthority`). A game's own messages take ids from 1200
   (`CoopProtocol.FirstGameMessageId`, `CoopGameMessages`; `CoopHost.On/Send/SendToAll`, `CoopClient.On/Send`).
   Status effects start and end, and cooldown starts, travel reliably with the world. `CoopScene` (1108, host-only)
   tells joined machines which scene the game is in (`ActionRpgHost.ChangeScene`).
5. **Refusals in plain words:** a different build ("The host plays a different version of the game"), a full party,
   nobody at that address (after about 5 seconds), the host leaving. The joined player is then back in a game of
   their own.
6. **Bad links.** Both sides ping every second; 2 seconds of silence shows "unstable" in the F2 panel, 10 seconds
   (`CoopProtocol.ConnectionTimeout`) counts as a lost link, whatever the transport (a crashed game or a pulled cable
   says nothing over UDP). The host then keeps the player's seat, name and health for 2 minutes
   (`CoopHost.RejoinWindow`; the seat counts as taken for newcomers), and the player's game joins again by itself, up
   to 3 times, with the token from its welcome: they are back in their seat with the health they had. If their game
   comes back before the host noticed, the old link is let go of at once. Leaving on purpose (Leave, F2) keeps no
   seat; the host closing ends the game for everyone, with no reconnecting.
7. **Broken or hostile messages** are dropped and counted; past 20 from one peer it is dropped. Counts no real message
   has are refused before anything is allocated, and positions that are not numbers are ignored.
8. **Saves.** The host's progress is saved on level-up, finished quests, every 60 seconds (**Autosave Seconds**) and on
   quit, so a crash loses at most a minute. With shared progress a joined player's own saved game is not touched while
   they play with someone else; with progress of their own (`PerPlayer`) only its progress section is written
   (`ActionRpgSaves.ForProgressOnly`).

## Connecting players: session providers

Transport (how bytes travel) and sessions (how a game is opened, found and joined) are separate. The game asks an
`INetworkSessionProvider` for a session and gets a ready `INetworkTransport` back; `CoopHost` / `CoopClient`, the
messages and the rules never see how players connected.

```
INetworkSessionProvider            HostAsync(options) -> HostedSession { Transport (listening), JoinCode }
                                   JoinAsync(joinCode) -> Transport (connecting)
  LanSessionProvider               UDP on the local network / a VPN; the code is "IP" or "IP:port"     (done)
  LoopbackSessionProvider          in memory, for tests                                                 (done)
  UnityRelaySessionProvider        room code from Unity Multiplayer Services, traffic through Unity Relay (module, needs the SDK)
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
| **Unity Relay** | free up to an average of 50 concurrent players a month and 3 GiB per player (150 GiB max), then $0.16 per average CCU and $0.09/GiB (US/EU); figures Unity published in 2023, confirm before launch | host shares a short join code | a Unity Gaming Services project linked to the Unity project; the Multiplayer Services SDK `com.unity.services.multiplayer` (sessions + Relay; authentication with anonymous sign-in) | small: the transport already is Unity Transport, Relay plugs into its settings. About 1-2 days with tests |
| Steam (Steam Networking Sockets + lobbies) | free relays (Valve's network, hides IPs); $100 Steam Direct fee per game | invite from the Steam friends list, or join a lobby | the game on Steam (app id), Steamworks.NET or Facepunch.Steamworks; players need Steam | medium: a new transport over Steam sockets plus a lobby provider. About 3-5 days |
| Epic Online Services (P2P + lobbies) | free (no fees for P2P, lobbies, accounts) | invite or lobby; works on any store | an Epic developer account, the PlayEveryWare EOS plugin, a login per player (device id is enough) | medium-large: new transport over EOS P2P, big plugin. About 4-6 days |
| Photon (Realtime / Fusion) | 20 CCU free for development only; 100 CCU free only for Fusion/Quantum games; paid plans above | room name or code | Photon account and SDK; its own networking model | poor fit: Photon wants to be the whole netcode, competing with `ActorWorld` replication |
| Own relay + lobby server | a small VPS (a few dollars a month) plus our time | room code | a server we run, update and keep secure | large: 1-2 weeks, then operations |

Recommendation: **Unity Relay next**, as `RPGFramework.Networking.UnityRelay` (compiled only when the Relay package is
installed, like the Addressables provider). It is the smallest step and store-independent. Unity has offered a free tier
for Relay; whether it still exists and its limits must be checked against Unity's current terms before a game ships. Steam and EOS stay possible later as providers of their own, for a game that ships there; nothing in the
co-op changes for them. To start it, the project needs a Unity Gaming Services project (Edit > Project Settings >
Services), which is the owner's decision and account.

Sources: [Unity Gaming Services pricing](https://unity.com/products/gaming-services/pricing),
[How is the Relay service priced](https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced),
[Steam networking](https://partner.steamgames.com/doc/features/multiplayer/networking),
[EOS plugin for Unity](https://github.com/EOS-Contrib/eos_plugin_for_unity/blob/stable/com.playeveryware.eos/README.md),
[Photon pricing](https://doc.photonengine.com/photon/current/pricing).

## Unity Relay: compatibility check, adaptation plan and module (2026-10-26)

**Verdict: compatible.** Our `UnityNetworkTransport` already is a Unity Transport `NetworkDriver`; Relay is a setting
of that driver, not another transport. The protocol, the messages, `CoopHost` / `CoopClient` and the game rules do
not change. One class needs a small opening.

What Relay needs from a Unity Transport driver, against what ours does now:

| Step | Relay | `UnityNetworkTransport` today | Change |
|---|---|---|---|
| Driver settings | `NetworkSettings.WithRelayParameters(ref RelayServerData)` before `NetworkDriver.Create` | builds its own `NetworkSettings` (fragmentation 64 KB, reliable window 64, connect timeout) | let a caller add settings: a constructor taking `Action<NetworkSettings>` |
| Host | `Bind(NetworkEndpoint.AnyIpv4)` then `Listen()`; the driver registers with the relay | `Bind(AnyIpv4.WithPort(port))`, `Listen()` | `StartServer(0)` already does this |
| Client | `Connect(relayServerData.Endpoint)` | `Connect(string address, int port)` parses an IP | add `Connect(NetworkEndpoint)` |
| Pipelines, fragmentation, reliable-ordered, connection ids, events | unchanged | unchanged | none |

Where Relay comes from, current integration for Unity 6 (checked in Unity's registry today): the **Multiplayer
Services SDK** `com.unity.services.multiplayer` **2.4.1** (Unity 6000.0+). It bundles Relay, Lobby and Matchmaker
under *sessions* and depends on `com.unity.services.core` 1.18, `com.unity.services.authentication` 3.8 and
**`com.unity.transport` 2.7.4**. It does not need Netcode for GameObjects: a session can take our own network
handler (`SessionOptions.WithRelayNetwork().WithNetworkHandler(handler)`). The SDK then calls
`INetworkHandler.StartAsync(NetworkConfiguration)`, whose `Role` and `RelayServerData` (host) / `RelayClientData`
(client) are exactly what the driver settings above need. The session's short code becomes our join code.

Plan, in order (each step proven by tests before the next):
1. **Unity Transport 2.7.4** (done 2026-10-26): installing the SDK raises Unity Transport from 2.5.3 to 2.7.4 in
   the project (with Collections 2.6.8, Burst 1.8.29). With those pinned in a copy of the project, the whole suite
   passed unchanged at `069458d`: EditMode 1285/1285, PlayMode 7/7 (co-op over real UDP, eight games), no
   obsolete-API warnings in our code. The package keeps requiring 2.5.3 as its minimum; nothing forces 2.7.4 on
   projects without the SDK.
2. **Transport opening** (done; framework core, no new dependency, `RelayServerData` is part of Unity Transport):
   `new UnityNetworkTransport(DriverSettings)` adds to the driver settings; `Connect(NetworkEndpoint)`;
   `UnityNetworkTransport.ThroughRelay(RelayServerData)`; `RelayStatus` (NotUsingRelay / NotEstablished /
   Established / AllocationInvalid) so a provider can wait for the relay to accept the host. Tests on 2.5.3 and
   2.7.4: extra settings reach every driver and UDP works as before; a relay transport uses the relay and waits
   for its acceptance (no relay at a TEST-NET address, so it stays NotEstablished). LAN tests unchanged.
3. **Module `RPGFramework.Networking.UnityRelay`** (own assembly, compiled only when `com.unity.services.multiplayer`
   is installed, like the Addressables provider): `UnityRelaySessionProvider : INetworkSessionProvider`.
   `HostAsync` signs in anonymously, creates a session (max players from `SessionOptions`, DTLS) and returns the
   transport with the session code; `JoinAsync(code)` joins by code. Service errors become
   `NetworkSessionException` in plain words (no such room, room full, no internet, services not set up). The cloud
   calls sit behind a small internal interface so they can be faked in tests.
4. **Setup check:** before hosting, the provider checks that the project is linked to a Unity Gaming Services
   project (`Application.cloudProjectId` not empty) and says what to do if not: Edit > Project Settings > Services,
   link or create a project, keep anonymous sign-in on (default). Same text in recipe 11.
5. **Tests in CI:** with the faked cloud: create, join, wrong code, full room, services not set up, a different game
   version still refused by our fingerprint after a relay join; LAN regression: the existing LAN and co-op tests run
   unchanged. Real relay traffic needs Unity's servers and the owner's project, so it is a manual check (two
   machines on different networks), optionally a nightly job if the project's credentials are added as CI secrets.
6. **Panel:** F2 offers "Local network" and "Internet (Unity Relay)" when the module is present; the LAN provider stays
   the default.

**Step 3 status: module built.** `Networking/UnityRelay` (assembly `RPGFramework.Networking.UnityRelay`, define
`RPG_MULTIPLAYER_SERVICES` set by `versionDefines` when `com.unity.services.multiplayer` 2.4.1+ is installed):
`IRelayService` (the cloud side; `MultiplayerRelayService` is the real one: anonymous sign-in, private session, Relay
over DTLS, our `INetworkHandler`), `UnityRelaySessionProvider` (host waits until the relay accepts it, codes are
case- and dash-insensitive, failed starts close the driver and the room), `RelaySessionTransport` (closing the game
leaves the room; a host's room closes for everyone), `RelayErrors` (service errors in plain words). The provider
registers itself in `NetworkSessionProviders`; `ActionRpgHost` lists registered providers next to the local network
and F2 lets the player choose. The main project now has the SDK, so CI runs the relay tests.

What the tests prove, and what they do not: `UnityRelaySessionProviderTests` run the provider against a **fake** room
service over the in-memory network: create, code, join, connect, co-op plays, a different game version is refused,
wrong code, full room, empty code, project not set up, leaving, a transport that cannot open, and the real Unity
Transport host giving up when no relay answers. They prove the integration logic. They do **not** prove that two
players on different networks can play: that needs Unity's servers, a linked UGS project and a manual test (host on
one network, friend on another: code, join, both see movement, combat, then the host leaves and the friend is told).

What it needs from the owner: a Unity Gaming Services project linked to this Unity project. Check the current
Relay pricing and free tier first (the figures in the table above are from 2023). Steps 1-2 need no account.

Sources: [Network connection management (Multiplayer Services)](https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/manage-session-network-connection),
[`INetworkHandler`](https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.4/api/Unity.Services.Multiplayer.INetworkHandler.html),
[`NetworkConfiguration`](https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.4/api/Unity.Services.Multiplayer.NetworkConfiguration.html),
[Use Relay with UTP](https://docs.unity.com/en-us/relay/relay-and-utp),
[`Unity.Networking.Transport.Relay`](https://docs.unity3d.com/Packages/com.unity.transport@2.5/api/Unity.Networking.Transport.Relay.html).

## Limits worth knowing

- Positions are trusted from each player for their own character (the host checks reach for attacks). Fine among
  friends; a competitive game would need server-side movement.
- No client-side prediction: a joined player's hits show after one round trip (a few milliseconds on a LAN).
- With shared progress, a friend who joins brings nothing and takes nothing home. With `PerPlayer` the host trusts
  the progress a friend brings (fine among friends).
- No host migration: when the host's game ends, the game ends for everyone (their own saved games are untouched).
- Reconnecting restarts the joined player's game (a second or two); `IRespawnPointProvider` (`CoopHost.RespawnPoints`)
  decides where they come back, otherwise the starting ring.
- A scene change (`ActionRpgHost.ChangeScene`) keeps the game, the party and the link; joined machines load the
  host's scene. Enemies and the players' positions come along as they were: the game places players for the new
  scene in `OnSceneChanged` and removes enemies it does not want there.

## Next, if wanted

- A Unity Relay session provider for playing over the internet without a VPN (see the comparison above).
