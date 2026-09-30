---
title: Deferred follow-ups (release, infrastructure, accounts, multiplayer)
tags: [plan, deferred, follow-ups, release, game, security, auth, email-verification, entra, postgres, monitoring, simultaneous-turns, multiplayer]
code: [src, infra, .github/workflows]
updated: 2026-09-30
---

# Deferred follow-ups

**Status: deferred (2026-09-30).** The game itself (UI design system, richer ruleset content and mechanics) comes first; the items below wait. Their priority and "revisit when" triggers are in the [backlog](../backlog.md), which is the only place priorities are kept. The details here are kept so an item can be picked up without rediscovering it.

The items were collected from the earlier plans (all [done](../done/)). Ids `F1`–`F10` keep the numbers of the former "next follow-ups" list (`F6`, the `UnitView` change, is done). Game ideas that were item 8 there are split: game log and animations, player-chosen city names and the scale-out backplane are now `F11`–`F13` in the backlog's Next list; turn timers stay here as `F8`.

When an item is picked up, it runs through `/develop-feature`: its own plan in `.claude/rules/planning.md` format under `docs/plans/active/`, a branch, and a PR. Everything, game work included, branches from and PRs to `stage` (`gamecore` was merged into `stage` and retired on 2026-09-30).

| Id | Item | Area | Size |
|---|---|---|---|
| F1 | [Ship the game](#f1-ship-the-game) | release | M |
| F2 | [Least-privilege database access](#f2-least-privilege-database-access) | infra | M |
| F3 | [Monitoring and alerts](#f3-monitoring-and-alerts) | infra | M |
| F4 | [Sign-up email inside the database transaction](#f4-sign-up-email-inside-the-database-transaction) | users | S |
| F5 | [Email verification](#f5-email-verification) | users, client | M |
| F7 | [Invite code rotation (kick)](#f7-invite-code-rotation-kick) | game | S |
| F8 | [Turn timers and absent players](#f8-turn-timers-and-absent-players) | game | M |
| F9 | [Simultaneous turns (Civ 6 style)](#f9-simultaneous-turns-civ-6-style) | game | M–L |
| F10 | [More than two players](#f10-more-than-two-players) | game | M |

## F1. Ship the game

- **Why:** the game (Plans 1–5) is on `stage` (merged from the retired `gamecore` on 2026-09-30). Players can't reach it until it is released.
- **Steps:**
  1. Run the `security-auditor` agent before the `stage` → `main` release PR. Critical or High findings block the release.
  2. Release. If this is the first deployment, follow [deployment](../../operations/deployment.md): the [template roadmap](../done/template-solution-roadmap.md) recorded Bicep and the workflows as written but not yet run.
- **Check after deploy (not covered locally):**
  - Play a game in the published Release build (Blazor trimming; `Govern.Core` is kept whole by `TrimMode` partial).
  - Confirm the board works under the Static Web App CSP with no violations in the console. The dev server doesn't send the CSP.

## F2. Least-privilege database access

- **Why:** the Api connects with the PostgreSQL administrator login, and the firewall allows all Azure services. The admin password is the only barrier ([deployment known gaps](../../operations/deployment.md#known-gaps-follow-ups)). Formerly follow-up #2.
- **Approach:** Microsoft Entra authentication with the Web App's system-assigned managed identity. A database role mapped to that identity owns only the `govern` objects, and the connection string holds no password. The admin password stays as break-glass only.
- **Detailed plan:** [follow-ups delivery, Plan 4](../done/follow-ups-delivery.md#plan-4-least-privilege-database-access-via-entra-managed-identity-2).

## F3. Monitoring and alerts

- **Why:** refresh-token theft (EventId 1001), brute force (401/429 spikes) and 5xx responses are only visible in container stdout. Formerly follow-up #5.
- **Approach:** one Log Analytics workspace, workspace-based Application Insights, and the Azure Monitor OpenTelemetry distro exporting Warning+ logs. Add alerts on EventId 1001, on 5xx rate and on 401/429 spikes, reusing the action group of the storage alert (`infra/modules/alerts.bicep`).
- **Detailed plan:** [follow-ups delivery, Plan 5](../done/follow-ups-delivery.md#plan-5-monitoring-and-alerts-5).

## F4. Sign-up email inside the database transaction

- **Why:** the sign-up email should be sent only when the user row commits, with no outbox or hosted job.
- **Detailed plan:** [session and email verification, Plan 2](../done/session-and-email-verification.md#plan-2-send-the-sign-up-email-inside-the-database-transaction).

## F5. Email verification

- **Why:** accounts are cheap today: there's no email verification, and sign-up is limited only per IP. That makes the game storage abuse (many accounts looping create → join → start → resign) possible over days. The seat quota and the storage alert bound it, and verification (or a daily sign-up cap) is the lasting fix (Medium, [game plan 2 follow-ups](../done/game-lobby.md#follow-ups-from-the-code-review-and-security-audit-before-stage--main)).
- **Scope:** the Api side, plus the client's confirm-email page (the client shipped first).
- **Detailed plan:** [session and email verification, Plan 3](../done/session-and-email-verification.md#plan-3-email-verification).

## F7. Invite code rotation (kick)

- **Why:** leaving a lobby doesn't rotate the invite code, so a player who left (or anyone the code leaked to) can take the free seat again. The creator's only recourse is to cancel and recreate (Low, Plan 3c audit).
- **Approach:** a creator-only "remove player" that frees the seat and rotates the code, with a lobby-panel button.
- **Also Low:** the lobby cap is a soft cap, because simultaneous creates can overshoot it.

## F8. Turn timers and absent players

- **Why:** a player who stops playing blocks the game, since turns alternate and nothing ends a turn for them. Formerly one of the item 8 game ideas.
- **Approach:** a per-turn time limit and a rule for absent players (auto end turn, then forfeit after n missed turns). Needs server background work.
- **Prerequisite for** [simultaneous turns](#f9-simultaneous-turns-civ-6-style) and [more than two players](#f10-more-than-two-players).

## F9. Simultaneous turns (Civ 6 style)

- **Why:** in Civ 6 all players act at once during a turn, and the round ends when everyone has ended their turn. Waiting for the opponent to finish every turn is slow. Today turns alternate: `GameState.CurrentPlayer` may act, and `CommandGuardRule` answers everyone else with `game.not_your_turn`.
- **Approach:** a turn mode chosen in the lobby (`Alternating`, the default, or `Simultaneous`) and stored in the game state.

| Area | Change |
|---|---|
| Core state | A per-player "ended this turn" flag replaces `CurrentPlayer` in simultaneous mode. The turn mode is stored in the state (schema v3; v2 games read as `Alternating`) |
| Turn rule | Every active player may act until they end their turn; the guard's error becomes "you already ended your turn" (optionally allow un-ending). When the last player ends, the round runs for everyone in a fixed order: production, reset movement, turn + 1 |
| Conflicts | First command to reach the server wins (two units to one tile, two attacks on one target); the second is checked against the new state and gets a normal rule error. Command order is total, so determinism and replay hold |
| View previews | `ViewPreviewCalculator.CheckTurn` uses the viewer's own "ended" flag; `GameEnginePreviewTests` gets simultaneous-mode scenarios |
| View | `PlayerView` shows "you ended your turn" and "the opponent ended their turn" (Civ 6 shows this openly, so it's an accepted leak) |
| Server | `current_player_id` and "Your turn" in the game list become "needs your action"; one migration |
| Client | The turn panel shows "Waiting for the opponent…", and End turn / Undo end turn |

- **Decide first:**
  - **Stale commands:** every visible enemy move changes your view fingerprint, so `expectedVersion` would reject many commands mid-skirmish. Recommended: make the version check advisory in simultaneous mode; the engine re-checks every command against the current state anyway.
  - **Double moves:** a unit can attack at the end of one turn and again at the start of the next, before the opponent reacts. Civ 6's answer is dynamic turns (simultaneous in peace, alternating at war); the stored turn mode leaves room for that later.
  - **Absent players:** a round ends only when everyone ends their turn, so do this together with [turn timers](#f8-turn-timers-and-absent-players), or after them.

## F10. More than two players

- **Why:** games are 1-vs-1 only. Free-for-all games of 3–4 players would add variety.
- **Already N-ready:**
  - turn order (`EndTurnRule.PassTurn` walks the player list and skips inactive players);
  - victory (`VictoryRule` ends the game when at most one active player remains);
  - fog of war (views are built per viewer, and `PlayerView.Players` is a list);
  - seats, SSE streams, the quota and the version fingerprint are per player.
- **Hard-coded for two:**

| Where | Today | Change |
|---|---|---|
| The ruleset's `setup.players` (the validator requires 2) and the `GameFactory` check | exactly 2 | a range of 2–N (say 4) in the ruleset |
| `GameFactory.SpawnTiles(isLeftSide)` | left or right quarter of the map | spawn spots spread over the map (corners or a ring) with a minimum distance, still drawn from the seeded generator |
| Map size | 20×14 default | scales with the player count, or the creator chooses |
| `Domain/Entities/Game.MaxPlayers` / `IsFull` | = `PlayerCount` | max seats chosen at create time (min 2), stored in a column; one migration |
| Start rule | full lobby | start once at least 2 have joined, or keep requiring a full lobby (decide) |
| Client | "n/2", start needs 2, "Your opponent", own/enemy colours | "n/max", one colour per seat, "Player 2 (red)" labels in the panels, game over lists every player's result |
| Tests | two-player scenarios | 3–4 players: turn order, elimination mid-game, resigning out of turn, each player's fog |

- **Decide first:**
  - **Waiting:** with alternating turns, each player waits for all the others every round. Do this with or after [simultaneous turns](#f9-simultaneous-turns-civ-6-style) and [turn timers](#f8-turn-timers-and-absent-players).
  - **Free-for-all dynamics:** ganging up and kingmaking are expected; teams or diplomacy would be a separate feature.
  - **Resigning mid-game:** the game continues for the others. Check that the current player resigning on their own turn passes the turn with 3+ players.
  - **Start below the max:** the invite code stays valid until start, which makes [invite code rotation](#f7-invite-code-rotation-kick) a bit more relevant.
