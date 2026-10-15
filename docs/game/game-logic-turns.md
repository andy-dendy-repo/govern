---
title: Game logic, simultaneous turns (turn mode, ending and taking back a turn, resolving a round, conflicts)
tags: [game, core, rules, algorithms, turns, rounds, simultaneous, sequential, turn-mode, end-turn, resume-turn, conflicts, f9]
code: [src/Govern.Core/State/TurnMode.cs, src/Govern.Core/Engine/RoundRule.cs, src/Govern.Core/Engine/CommandGuardRule.cs, src/Govern.Core/Actions/EndTurnAction.cs, src/Govern.Core/Actions/ResumeTurnAction.cs, src/Govern.Core/Engine/VictoryRule.cs]
updated: 2026-10-15
---

# Game logic: simultaneous turns

F9. A game's turn mode (`GameState.TurnMode`) is chosen in the lobby and never changes. `sequential` (the default, and what every game saved before F9 reads as) is the one-player-at-a-time game in [game logic](game-logic.md#end-turn). This page is the `simultaneous` mode.

## Terms

| Term | Meaning |
|---|---|
| Turn mode | `sequential` or `simultaneous`; left out of the stored JSON for a sequential game, so no schema change |
| Ended turn | `Player.EndedTurn`: the player has ended this round. Public: `PlayerSummary.EndedTurn` in every player's view |
| May act | `GameState.MayAct` / `PlayerView.MayAct`: sequential, the current player; simultaneous, an active player who hasn't ended the round |
| Current player | Simultaneous: the first active seat. It decides nothing (no rule reads it in this mode) |

## The guard in a simultaneous game

1. The game is finished → `game.finished`; the actor isn't active → `game.not_a_player`.
2. Resign is always allowed. Resume turn passes the guard (its own rule checks it).
3. Any other command from a player who has ended the round → `game.turn_ended`.

The view-based previews use the same check with the viewer's own flag, so a player who has ended sees no moves or attacks.

## End turn and Resume turn

- `end_turn` sets the actor's `EndedTurn` and emits `player_ended_turn` (`ended: true`). Nothing else happens yet: no income, no production, no movement.
- `resume_turn` (Undo end turn) clears it and emits `player_ended_turn` (`ended: false`). Only in a simultaneous game (`game.not_simultaneous`) and only after ending (`game.turn_not_ended`).

## Resolving the round (RoundRule)

After every accepted command, once elimination and victory are decided, if the game goes on and every active player has ended:

1. For each active player in seat order, run steps 1 to 7 of [End turn](game-logic.md#end-turn) for them (resting units, jobs, income, upkeep, trade, territory, production). Nobody's movement has been reset yet, so a unit rests by what it did this round.
2. Cut every city's stock to its storage caps once (step 8).
3. The round increases by 1 (`round_ended`, with the new round); the current player becomes the first active seat; every `EndedTurn` is cleared.
4. Every active player's units get their full movement.
5. Every active player's units with a destination or exploring walk on, player by player in seat order.

So the round also resolves when the last player still playing resigns or is eliminated. A seat that leaves the game during the round makes the current player the first active seat; the round goes on.

## Conflicts

Commands are applied one at a time, in the order the server saves them (the game's `revision`), and each is checked against the state it lands on. The first command wins:

| Situation | Second command gets |
|---|---|
| Two units sent to one free tile | `move.occupied` (or its path stops where the tile is seen taken) |
| An attack on a unit that already died or moved | `attack.no_target` |
| A unit already captured city, or a deal already answered | the normal rule error for the new state |

The order is total, so a game replays to the same state. A unit can attack at the end of one round and again at the start of the next before its target reacts; that is accepted for now.

## Hosting

The server skips the view-version check for simultaneous games and retries a command that loses the save race on the newer game ([games feature](../features/games.md#simultaneous-turns-turnmode)).

What a player learns about the others in a simultaneous game, beyond their own view:

| Signal | Accepted because |
|---|---|
| Who has ended their turn (the player strip, `player_ended_turn` events) | Shown openly on purpose: it tells you whom the round waits for |
| A command answered slowly or rejected as stale after its retries | Only says that some other player acted at that moment, never what they did; every command is still checked against the fogged state the sender may see |
