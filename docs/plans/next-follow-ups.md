---
title: Next follow-ups (after the game plans)
tags: [plan, backlog, follow-ups, release, game, security, auth, email-verification, entra, postgres, monitoring]
code: [src, infra, .github/workflows]
updated: 2026-09-30
---

# Next follow-ups

Future game mechanics are listed separately in the [game design roadmap](game-design-roadmap.md). All earlier plans are done: the [template roadmap](template-solution-roadmap.md), the [follow-ups backlog](follow-ups.md) and its [delivery plans](follow-ups-delivery.md), [session and email verification](session-and-email-verification.md) Plan 1, and game Plans 1–5 ([game delivery](game-delivery.md), [game plan 2](game-lobby.md)). This file holds everything still open, collected from those plans.

Run each item through `/develop-feature`. It gets its own plan in `.claude/rules/planning.md` format, a branch, and a PR. Game work branches from and PRs to `gamecore` until the game ships; everything else branches from `stage`. When an item is done, remove it here and update the related docs.

| # | Priority | Item | Area | Size |
|---|---|---|---|---|
| 1 | High | [Ship the game](#1-ship-the-game) | release | M |
| 2 | High | [Least-privilege database access](#2-least-privilege-database-access) | infra | M |
| 3 | Medium | [Monitoring and alerts](#3-monitoring-and-alerts) | infra | M |
| 4 | Medium | [Sign-up email inside the database transaction](#4-sign-up-email-inside-the-database-transaction) | users | S |
| 5 | Medium | [Email verification](#5-email-verification) | users, client | M |
| 6 | Low | [Hide a visible enemy unit's movement](#6-hide-a-visible-enemy-units-movement-unitview) | game | S |
| 7 | Low | [Invite code rotation (kick)](#7-invite-code-rotation-kick) | game | S |
| 8 | — | [Game ideas](#8-game-ideas) | game | S–L each |
| 9 | — | [Simultaneous turns (Civ 6 style)](#9-simultaneous-turns-civ-6-style) | game | M–L |
| 10 | — | [More than two players](#10-more-than-two-players) | game | M |

## 1. Ship the game

- **Why:** the game (Plans 1–5) lives on `gamecore`. Players can't reach it until it is released.
- **Steps:**
  1. Merge PR #17 (client board) into `gamecore`.
  2. Open a PR from `gamecore` to `stage`.
  3. Run the `security-auditor` agent before the `stage` → `main` release PR. Critical or High findings block the release.
  4. Release. If this is the first deployment, follow [deployment](../operations/deployment.md): the [template roadmap](template-solution-roadmap.md) recorded Bicep and the workflows as written but not yet run.
- **Check after deploy (not covered locally):**
  - Play a game in the published Release build (Blazor trimming; `Govern.Core` is kept whole by `TrimMode` partial).
  - Confirm the board works under the Static Web App CSP with no violations in the console. The dev server doesn't send the CSP.

## 2. Least-privilege database access

- **Why:** the Api connects with the PostgreSQL administrator login, and the firewall allows all Azure services. The admin password is the only barrier ([deployment known gaps](../operations/deployment.md#known-gaps-follow-ups)). Formerly follow-up #2.
- **Approach:** Microsoft Entra authentication with the Web App's system-assigned managed identity. A database role mapped to that identity owns only the `govern` objects, and the connection string holds no password. The admin password stays as break-glass only.
- **Detailed plan:** [follow-ups delivery, Plan 4](follow-ups-delivery.md#plan-4-least-privilege-database-access-via-entra-managed-identity-2).

## 3. Monitoring and alerts

- **Why:** refresh-token theft (EventId 1001), brute force (401/429 spikes) and 5xx responses are only visible in container stdout. Formerly follow-up #5.
- **Approach:** one Log Analytics workspace, workspace-based Application Insights, and the Azure Monitor OpenTelemetry distro exporting Warning+ logs. Add alerts on EventId 1001, on 5xx rate and on 401/429 spikes, reusing the action group of the storage alert (`infra/modules/alerts.bicep`).
- **Detailed plan:** [follow-ups delivery, Plan 5](follow-ups-delivery.md#plan-5-monitoring-and-alerts-5).

## 4. Sign-up email inside the database transaction

- **Why:** the sign-up email should be sent only when the user row commits, with no outbox or hosted job.
- **Detailed plan:** [session and email verification, Plan 2](session-and-email-verification.md#plan-2-send-the-sign-up-email-inside-the-database-transaction).

## 5. Email verification

- **Why:** accounts are cheap today: there's no email verification, and sign-up is limited only per IP. That makes the game storage abuse (many accounts looping create → join → start → resign) possible over days. The seat quota and the storage alert bound it, and verification (or a daily sign-up cap) is the lasting fix (Medium, [game plan 2 follow-ups](game-lobby.md#follow-ups-from-the-code-review-and-security-audit-before-stage--main)).
- **Scope:** the Api side, plus the client's confirm-email page (the client shipped first).
- **Detailed plan:** [session and email verification, Plan 3](session-and-email-verification.md#plan-3-email-verification).

## 6. Hide a visible enemy unit's movement (UnitView)

- **Why:** enemy units on visible tiles are sent as Core's `Unit`, so the view JSON includes their `MovementLeft`. The board hides it, but devtools show how far the unit moved through fog, or that it attacked (Low, client board security audit).
- **Approach:** project a `UnitView` in `VisibilityCalculator.ViewFor` with `MovementLeft` null for enemy units, as `CityView` hides enemy production. Keep the view previews' parity tests green, and update the client's `UnitPanel` and `TestViews`.

## 7. Invite code rotation (kick)

- **Why:** leaving a lobby doesn't rotate the invite code, so a player who left (or anyone the code leaked to) can take the free seat again. The creator's only recourse is to cancel and recreate (Low, Plan 3c audit).
- **Approach:** a creator-only "remove player" that frees the seat and rotates the code, with a lobby-panel button.
- **Also Low:** the lobby cap is a soft cap, because simultaneous creates can overshoot it.

## 8. Game ideas

Deferred from the [game delivery plan](game-delivery.md). None is scheduled. Bigger game mechanics (terrain, resources, rivers, city territory and stats, governors, unit roster and HP combat, builders and buildings, science and culture trees, seasons), plus the UI design system and the game rules reference, are in the [game design roadmap](game-design-roadmap.md).

| Idea | Size | Notes |
|---|---|---|
| Game log and animations from engine events filtered by visibility (`VisibilityCalculator.EventsFor`) | M–L | Core first, then client UI |
| Player-chosen city names | S–M | Also closes a small leak: `City N` counts the founder's cities at founding, hidden ones included |
| Turn timers and handling of absent players | M | Needs server background work; a prerequisite for [simultaneous turns](#9-simultaneous-turns-civ-6-style) |
| Scale-out beyond one Api instance (Postgres `LISTEN/NOTIFY` as the SSE backplane) | M | Only needed with more than one instance |

## 9. Simultaneous turns (Civ 6 style)

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
  - **Absent players:** a round ends only when everyone ends their turn, so do this together with turn timers ([game ideas](#8-game-ideas)), or after them.

## 10. More than two players

- **Why:** games are 1-vs-1 only. Free-for-all games of 3–4 players would add variety.
- **Already N-ready:**
  - turn order (`EndTurnRule.PassTurn` walks the player list and skips inactive players);
  - victory (`VictoryRule` ends the game when at most one active player remains);
  - fog of war (views are built per viewer, and `PlayerView.Players` is a list);
  - seats, SSE streams, the quota and the version fingerprint are per player.
- **Hard-coded for two:**

| Where | Today | Change |
|---|---|---|
| `Core/State/GameSetup.PlayerCount` and the `GameFactory` check | exactly 2 | a player count of 2–N (say 4) in `GameSetup` |
| `GameFactory.SpawnTiles(isLeftSide)` | left or right quarter of the map | spawn spots spread over the map (corners or a ring) with a minimum distance, still drawn from the seeded generator |
| Map size | 20×14 default | scales with the player count, or the creator chooses |
| `Domain/Entities/Game.MaxPlayers` / `IsFull` | = `PlayerCount` | max seats chosen at create time (min 2), stored in a column; one migration |
| Start rule | full lobby | start once at least 2 have joined, or keep requiring a full lobby (decide) |
| Client | "n/2", start needs 2, "Your opponent", own/enemy colours | "n/max", one colour per seat, "Player 2 (red)" labels in the panels, game over lists every player's result |
| Tests | two-player scenarios | 3–4 players: turn order, elimination mid-game, resigning out of turn, each player's fog |

- **Decide first:**
  - **Waiting:** with alternating turns, each player waits for all the others every round. Do this with or after [simultaneous turns](#9-simultaneous-turns-civ-6-style) and turn timers ([game ideas](#8-game-ideas)).
  - **Free-for-all dynamics:** ganging up and kingmaking are expected; teams or diplomacy would be a separate feature.
  - **Resigning mid-game:** the game continues for the others. Check that the current player resigning on their own turn passes the turn with 3+ players.
  - **Start below the max:** the invite code stays valid until start, which makes [invite code rotation](#7-invite-code-rotation-kick) a bit more relevant.
