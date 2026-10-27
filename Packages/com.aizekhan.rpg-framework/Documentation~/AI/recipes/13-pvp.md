# Recipe: PvP (duel, team deathmatch, free-for-all)

PvP is co-op with a match on top: players join the same way (F2, `-host`, `-join`), then fight each other.

| Task | Do |
|---|---|
| Turn it on | Game Setup > **PvP** > **Enabled**. Pick the **Mode**: **Duel** (rounds, no respawn within a round, first to **Rounds To Win**), **Team Deathmatch** (teams respawn, first team to **Score To Win** defeats), **Free For All** (everyone alone, first to **Score To Win**). |
| Teams | **Teams** lists their names (Red, Blue). Joining players go to the team with fewest players; in Free For All each player is a side of their own ("P1", "P2"...). Name tags and rings take the side's colour. |
| Where players start | **Spawn Points**, one per side in order; empty puts the sides around a circle of **Arena Radius**. Teammates stand **Spacing** apart. Players go there when a round starts and when they respawn, on every machine. |
| Timing | **Respawn Delay**, **Round Break** (Duel), **Time Limit** (0 = none; the higher score wins, a tie is a draw), **Restart Delay** (a new match starts by itself; 0 = only from code). |
| Enemies too | Waves still work: enemies fight every player, players of the same side never hurt each other. Turn **Waves** off for pure PvP. |
| Teams that are factions | `settings.Hostility = new FactionHostility(factionRegistry)` and faction ids as team names: factions at war (Enemy, Rival) fight; allied, trading and neutral ones do not. |
| Your own rule of who hits whom | Implement `IHostilityRules` (`AreHostile(attacker, target)`) and set `ActionRpgSettings.Hostility`. `ActorWorld.Attack` refuses every attack it calls friendly; the co-op host checks friends' attacks with it too. |
| Code | `module.Pvp` is the `PvpMatch`: `Phase` (Waiting, Playing, RoundOver, Finished), `Round`, `ScoreOf(side)`, `RoundWinsOf(side)`, `Winner`, `TimeLeft`, `Restart()`. On any machine, `IActorWorld.PvpStatus` has the same for the HUD (null when not PvP). |
| React | `PlayerDefeatedEvent` (any fall; `ByPlayer` for PvP), `ActorTeamChangedEvent`, `PvpRoundStartedEvent`, `PvpScoreChangedEvent`, `PvpRespawnedEvent`, `PvpRoundEndedEvent`, `PvpMatchEndedEvent` (`Winner` null on a draw, `Forfeit` when the others left). |

How it stays fair:

- **The host decides everything**: hostility, damage, score, respawns. A modified client cannot hit a teammate or
  revive itself: the host refuses those intents.
- **Movement is checked.** A friend's position may not move faster than their character's **Move Speed**
  (`CharacterData.MoveSpeed`) allows, with 25 % and half a unit of slack (`CoopHost.MoveSpeedTolerance`,
  `CoopHost.MoveSlack`). Farther than that, the host keeps them where they could have got to and puts them there on
  their own machine (`CoopCorrectionMessage`; `CoopHost.MovementCorrections`, `CoopClient.Corrections` count them).
  This holds in co-op too. A character with Move Speed 0 is not checked.
- **Moving a player on purpose** (a spawn point, a portal): call `CoopHost.Teleport(playerId, pose)` on the host. Moving
  a friend's view on their machine directly looks like cheating and is corrected.
- The match rules are part of the build fingerprint: a host and a friend with different PvP settings refuse each other.

Tests: `PvpTests` (the match rules), `CoopTests` (`Pvp_...`, `MovingFasterThanTheCharacterCan_...`) and
`CoopPlayTests.Pvp_...` (two games over real UDP meet at their spawn points and duel).
