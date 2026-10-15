---
title: "Plan: F9 simultaneous turns"
tags: [plan, done, f9, turns, simultaneous, lobby]
code: [src/Govern.Core/State/GameState.cs, src/Govern.Core/Actions/EndTurnAction.cs, src/Govern.Core/Engine/CommandGuardRule.cs, src/Govern.Core/Engine/VictoryRule.cs, src/Govern.Domain/Entities/Game.cs, src/Govern.Application/Games, src/Govern.Client/Features/Games]
updated: 2026-10-15
---

# Plan: F9 simultaneous turns

Status: done 2026-10-15. Approved by the owner on 2026-10-15 with every default below (Undo end turn in, double moves accepted, no turn timers). Plan, implementation and close-out are one PR.

## Context
Turns alternate: only `GameState.CurrentPlayer` may act, so with 3 or 4 players (F10) everyone waits for everyone. The owner asked for simultaneous turns. Outcome: the creator picks the turn mode in the lobby; in a simultaneous game every player acts at once, presses End turn when done, and the round resolves when the last active player has ended.

## Scope
- In:
  - Lobby option `turnMode`: `sequential` (default, today's game) or `simultaneous`; stored on the game and in its state.
  - Simultaneous round: everyone acts, End turn marks you done (you can't act until the next round), **Undo end turn** while others still play. The round resolves when every active player has ended.
  - Round resolution in seat order: each player's end of turn (jobs, yields, upkeep, trade, territory, production) runs as today, then storage caps, then everyone gets full movement and their auto moves walk (seat order), turn + 1.
  - Conflicts: first command to reach the server wins. The engine checks each command against the state it is applied to, so a second unit sent to a now-taken tile gets the normal "tile occupied" error, an attack on a unit that already died gets "no target". Order is total (one game, one revision), so replay stays deterministic.
  - Live play: in simultaneous mode the view-fingerprint `expectedVersion` check is advisory (skipped), since every visible enemy move would make commands stale; a lost revision race is retried on the server instead of answering "stale".
  - Views: who has ended this round is public (players strip and top bar: "2 of 4 ended", "Waiting for others…"). Game list "Your turn" means "you haven't ended this round".
  - Leaving mid-round: if a resign or elimination leaves only ended players, the round resolves at once.
- Out:
  - F8 turn timers: an absent player still stalls the round, exactly as they stall a sequential game today.
  - Dynamic turns (alternating at war). The stored mode leaves room for it.
  - Any special rule for "double moves" (attack at the end of one round and again at the start of the next); see risks.

## Affected areas
- Core: `State/TurnMode.cs` (new), `State/GameState.cs` (`TurnMode`, default `Sequential` so stored games read unchanged, no schema bump), `State/Player.cs` (`EndedTurn`), `State/GameSetup.cs`, `State/GameFactory.cs`, `Engine/CommandGuardRule.cs` (`MayAct`), `Actions/EndTurnAction.cs` (split player end of turn from round start), `Actions/ResumeTurnAction.cs` (new, Undo end turn), `Engine/VictoryRule.cs`, `Engine/RuleViolations.cs` (`turn.ended`), `Visibility/PlayerView.cs`, `PlayerSummary.cs`, `VisibilityCalculator.cs`, `ViewPreviewCalculator.cs`, `Events/PlayerEndedTurnEvent.cs` and `Events/RoundEndedEvent.cs` (new), `Engine/RoundRule.cs` (new).
- Domain: `Entities/Game.cs` (`TurnMode`, `CurrentPlayerId` null in simultaneous mode), `Entities/GamePlayer.cs` (`EndedTurn` copy for the list query).
- Application: `CreateGameContract.cs` (`Turns`), `SendGameCommandContract.cs` (advisory version, retry), `ListMyGamesContract.cs`, `GetGameContract.cs`/models (turn mode in lobby and game).
- Infrastructure: configurations + migration `AddGameTurnMode`.
- Client: create-game form (turn mode picker), lobby/list labels, `TopBar`, `PlayerStrip`, `ActionBar`, `CityPanel`, `BoardClickResolver`, `UnitPanel` (all `CurrentPlayer == Viewer` checks go through one `view.MayAct`).

## Steps
1. Core: turn mode, ended flags, `MayAct`, End turn / Undo end turn, round resolution, resign/elimination close-out, views and previews.
2. Domain + Infrastructure: `Game.TurnMode`, `GamePlayer.EndedTurn`, migration.
3. Application + Api: create request, list "your turn", advisory version + retry.
4. Client: picker, labels, buttons, `MayAct`.
5. Docs: `docs/game/rules.md` (turns section), feature docs, close-out (plan to done, backlog).

## Tests
- Unit (Core): both players act in one round; ended player is rejected with `turn.ended`; Undo end turn; last End turn resolves every player's economy in seat order and resets everyone's movement; two units to one tile (second rejected); attack on a dead target; resign/elimination of the last unready player resolves the round; sequential games unchanged; old JSON reads as sequential; previews in simultaneous mode.
- Integration: create with `turns: simultaneous` (+ 400 on a bad value); two players send commands in the same round; list shows "your turn" until you end; stale fingerprint not enforced in simultaneous mode.
- Client (bUnit): picker, "Waiting for others", Undo end turn, buttons enabled for both players.
- Architecture: none, existing rules cover it.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green; a two-browser play-through in the client.

## Risks & open questions
- **Double moves:** a unit can attack at the end of a round and again at the start of the next before its target reacts. Default: accept it for now and see in play.
- **Undo end turn:** included by default (cheap); drop it if End turn should stay final.
- **Absent players:** without F8 a player who walks away stalls the round, same as today.

## As built

- Core: `TurnMode` on `GameState` (written only when simultaneous), `Player.EndedTurn`, `GameState.MayAct`/`PlayerView.MayAct`. `EndTurnRule` marks the player in a simultaneous game; `RoundRule` (after `VictoryRule` in every command) resolves the round with `EndTurnRule.ResolveRound`. `ResumeTurnCommand` (`resume_turn`) is Undo end turn. New codes `game.turn_ended`, `game.turn_not_ended`, `game.not_simultaneous`; new public events `player_ended_turn`, `round_ended`. Rules: [game logic: simultaneous turns](../../game/game-logic-turns.md).
- Server: `games.turn_mode`, `game_players.ended_turn` (migration `AddGameTurnMode`); `current_player_id` is null in a simultaneous game. `SendGameCommandHandler` skips the view-version check there and retries a lost save race up to 3 times. The rule that Api bodies carrying Core types need `BindAsync` now exempts Core enums (`NamingConventionTests`, `api-endpoints.md`).
- Client: a "One at a time / Everyone at once" picker on My games, "Turns" in the lobby, the players strip in every simultaneous game with "(ended)", the top bar's "waiting for…" text, Undo end turn, and every "my turn" check through `PlayerView.MayAct`.
