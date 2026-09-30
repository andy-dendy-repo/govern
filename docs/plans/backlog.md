---
title: Backlog (all open work, prioritised)
tags: [plan, backlog, priorities, roadmap, deferred, game, ui, ruleset, content]
code: [docs/plans, docs/game/design-roadmap.md]
updated: 2026-09-30
---

# Backlog

The one list of open work and the only place priorities are kept. Details live elsewhere and are linked: game mechanics in the [game design roadmap](../game/design-roadmap.md) (what each G item is), release, infrastructure and multiplayer items in [deferred follow-ups](deferred/follow-ups.md). How the backlog is kept is in `.claude/rules/planning.md` ("Backlog").

## Buckets and statuses

| Bucket | Meaning |
|---|---|
| **Now** | Being worked on, or next to start. At most 3 items, in order |
| **Next** | Agreed; starts when a Now slot frees. In order |
| **Deferred** | Decided not to do now. Each item has the date, the reason and a "revisit when" trigger |
| **Ideas** | Not agreed as wanted yet |

Status: `needs decision` (a question for the owner), `needs plan`, `planned` (a plan in `docs/plans/active/`), `in progress` (branch or PR), `blocked by <id>`.

Ids never change and are never reused: `F` for follow-ups, `G` for game design roadmap items, `C` for content slices.

## Now

| # | Id | Item | Area | Status | Detail |
|---|---|---|---|---|---|
| 1 | G1 | UI design system: an expandable system that grows with every new mechanic. UI only for now | client | in progress (phases 1–4: branch `claude/project-thread-pokkxe`; phase 5 lenses blocked by G4) | [plan](active/G1-ui-design-system.md), [G1](../game/design-roadmap.md#g1-ui-design-system-and-approaches), [game UI](../features/game-ui.md) |
| 2 | C1 | Richer ruleset and Core: add content and mechanics to the embedded ruleset (`rulesets/default/v1.json`) and Core. C1 = G2, G4, G9 (agreed 2026-09-30) | core, ruleset | needs plan | [Content slice C1](#content-slice-c1-g2-g4-g9) |

### Content slice C1: G2, G4, G9

Agreed 2026-09-30. The ruleset stays as it is today: one embedded file, no database (see G3b under Deferred). New content goes into that file, the Core rules that read it, the [game data](../game/game-data.md) and [game logic](../game/game-logic.md) docs, and the client panels that show it. Each item gets its own plan in `docs/plans/active/`, in this order:

| # | Id | Item | Why now |
|---|---|---|---|
| 1 | G2 | [Game rules reference](../game/design-roadmap.md#g2-game-rules-reference) | Small, and every content item updates it, so it should exist first |
| 2 | G4 | [Terrain: more tile types](../game/design-roadmap.md#g4-terrain-more-tile-types) | The map is the base for everything else; a first set of base terrain, hills and forest changes play without new systems |
| 3 | G9 | [Army units and combat](../game/design-roadmap.md#g9-army-units-and-combat) | Health and a small roster with counters make fights interesting with today's cities; builds on G4's defence modifiers |

## Next

| # | Id | Item | Area | Size | Notes |
|---|---|---|---|---|---|
| 1 | F11 | Game log and animations from engine events filtered by visibility (`VisibilityCalculator.EventsFor`) | core, client | M–L | Core first, then client UI |
| 2 | F12 | Player-chosen city names | core, client | S–M | Also closes a small leak: `City N` counts the founder's cities at founding, hidden ones included ([game-core](../features/game-core.md)) |
| 3 | F13 | Scale-out beyond one Api instance (Postgres `LISTEN/NOTIFY` as the SSE backplane) | server | M | Only needed with more than one Api instance |

## Deferred

| Id | Item | Deferred | Why | Revisit when | Detail |
|---|---|---|---|---|---|
| G3b | Rulesets in the database: stored versions, pinning each game, serving them, selectable setups in the lobby | 2026-09-30 | Adds a table, a repository, caching and an endpoint with no player-visible gain while there is one ruleset | A balance change would alter running production games, or admin publishing or a lobby ruleset choice is wanted | [plan](deferred/ruleset-b-store-and-pin.md) |
| F1 | Ship the game | 2026-09-30 | The game gets richer content first | The owner decides the game is ready to release | [F1](deferred/follow-ups.md#f1-ship-the-game) |
| F2 | Least-privilege database access | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F2](deferred/follow-ups.md#f2-least-privilege-database-access) |
| F3 | Monitoring and alerts | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F3](deferred/follow-ups.md#f3-monitoring-and-alerts) |
| F4 | Sign-up email inside the database transaction | 2026-09-30 | Goes with F5 | With F5 | [F4](deferred/follow-ups.md#f4-sign-up-email-inside-the-database-transaction) |
| F5 | Email verification | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F5](deferred/follow-ups.md#f5-email-verification) |
| F7 | Invite code rotation (kick) | 2026-09-30 | Low risk before release | Before F1, or with F10 | [F7](deferred/follow-ups.md#f7-invite-code-rotation-kick) |
| F8 | Turn timers and absent players | 2026-09-30 | Prerequisite of F9 and F10, which are deferred | With F9 or F10 | [F8](deferred/follow-ups.md#f8-turn-timers-and-absent-players) |
| F9 | Simultaneous turns (Civ 6 style) | 2026-09-30 | Content and UI first | Alternating turns become the main complaint in play | [F9](deferred/follow-ups.md#f9-simultaneous-turns-civ-6-style) |
| F10 | More than two players | 2026-09-30 | Content and UI first | 1-vs-1 play is solid; after F8 and F9 | [F10](deferred/follow-ups.md#f10-more-than-two-players) |
| G5 | Resources and tile yields | 2026-09-30 | Not in content slice C1; needs G7/G8 (territory, city stats) to matter | C1 is done; then pick the next slice | [G5](../game/design-roadmap.md#g5-resources-and-tile-yields) |
| G6 | Rivers and bridges | 2026-09-30 | Not in content slice C1; edge-based pathfinding is a big change; bridges need G10 | C1 is done; then pick the next slice | [G6](../game/design-roadmap.md#g6-rivers-and-bridges) |
| G7 | City territory and growth | 2026-09-30 | Not in content slice C1; needs G5 | C1 is done; then pick the next slice | [G7](../game/design-roadmap.md#g7-city-territory-and-growth) |
| G8 | City stats and the production queue | 2026-09-30 | Not in content slice C1; needs G7 | C1 is done; then pick the next slice | [G8](../game/design-roadmap.md#g8-city-stats-and-the-production-queue) |
| G10 | Builders and buildings (with upgrades) | 2026-09-30 | Not in content slice C1; needs G7, G8 | C1 is done; then pick the next slice | [G10](../game/design-roadmap.md#g10-builders-and-buildings-with-upgrades) |
| G11 | Science and culture trees | 2026-09-30 | Not in content slice C1; needs G8 | C1 is done; then pick the next slice | [G11](../game/design-roadmap.md#g11-science-and-culture-trees) |
| G12 | Civilizations and governors | 2026-09-30 | Not in content slice C1; needs G8, G11 | C1 is done; then pick the next slice | [G12](../game/design-roadmap.md#g12-civilizations-and-governors) |
| G13 | Colonies founded by governors | 2026-09-30 | Not in content slice C1; needs G12 | C1 is done; then pick the next slice | [G13](../game/design-roadmap.md#g13-colonies-founded-by-governors-settlers-removed) |
| G14 | Seasons | 2026-09-30 | Not in content slice C1; modifies almost everything; last | C1 is done; then pick the next slice | [G14](../game/design-roadmap.md#g14-seasons) |
| G15 | Ships and naval movement | 2026-09-30 | Not in content slice C1; needs G6, G10 | C1 is done; then pick the next slice | [G15](../game/design-roadmap.md#g15-ships-and-naval-movement) |
| G16 | Diplomacy (war and peace, trade, alliances, diplomacy points) | 2026-09-30 | Not needed for 1-vs-1 | F10, or resource trading (G5) is wanted | [open question 1](../game/design-roadmap.md#open-questions) |

## Ideas

None yet. New ideas are added here as one line and promoted to Next or Now by the owner.

## Recently done

| Id | Item | Done | Plan |
|---|---|---|---|
| G3a | Core reads every game number from a loadable ruleset | 2026-09-30, PR #20 | [ruleset A](done/ruleset-a-core.md) |
| F6 | Hide a visible enemy unit's movement (`UnitView`) | 2026-09-30, PR #19 | — |
