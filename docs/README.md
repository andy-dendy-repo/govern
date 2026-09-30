---
title: Govern knowledge base index
tags: [index, docs]
code: [docs]
updated: 2026-09-30
---

# Govern knowledge base

The project's knowledge base, used for RAG. Every document has front matter (title, tags, code paths, updated date) and covers one topic. The writing rules are in `.claude/rules/docs.md`.

## Architecture
- [architecture.md](architecture.md): layers, allowed references, request flow, folder templates (UF / AGFT / 3DFT), naming rules and how they are enforced.

## Features
- [features/game-core.md](features/game-core.md): the pure game engine in `Govern.Core`: every number from the ruleset (`Ruleset`, a parameter of every entry point), hex map, units and abilities, cities, commands and rules, error codes, fog of war (`PlayerView` with row-major tiles, and hidden enemy production and movement), view-based previews for the client, the opaque view version, random ids, determinism and the JSON format (schema v2).
- [features/games.md](features/games.md): games and lobby: phases, `/api/games` endpoints and status codes, sending commands (opaque per-player `expectedVersion`, rule-violation codes), live updates over SSE (`/stream`, heartbeat, push on visible change), lobby leave/cancel and 7-day expiry, the list's `activityAt`, error codes, per-user rate limits, caps and the rolling 24-hour quota, invite codes, per-game player ids (no user ids exposed), join/start/command races, `games`/`game_players` tables and the `jsonb` state.
- [features/client.md](features/client.md): Blazor WASM client architecture (CFT feature folders), `Api:BaseUrl` configuration, authentication (token store, refresh ahead of expiry and on 401, cross-tab Web Lock, login/register/profile pages, login redirect), typed clients and `ApiError`, the game lobby (`/games`, `/join#<code>` invite links, live game page with SSE reconnect and backoff), the game board (SVG hex map with pan and zoom, selection and highlights from the Core previews, commands one at a time with stale-version reload, game over), CSP and security headers on the Static Web App, release-time configuration, bUnit and architecture tests.
- [features/users.md](features/users.md): sign-up, sign-in, refresh token rotation and reuse detection, session cap and cleanup, sign-out, current user; endpoints, errors, data model.

## Decisions
- [decisions/0001-mediator.md](decisions/0001-mediator.md): in-house mediator with `Result` responses and error-handling, logging and validation pipelines.
- [decisions/0002-auth-tokens.md](decisions/0002-auth-tokens.md): JWT access tokens, rotating refresh tokens, and client token storage.
- [decisions/0003-no-mapping-libraries.md](decisions/0003-no-mapping-libraries.md): EF projections and static `Projection`/`From` members instead of mapping libraries.
- [decisions/0004-game-engine.md](decisions/0004-game-engine.md): authoritative server with a pure, deterministic, dependency-free game engine; hex map, alternating turns, fog of war.
- [decisions/0005-live-updates.md](decisions/0005-live-updates.md): live game updates over SSE with per-player snapshots, an in-process change hub, an opaque view version and random ids.

## Game design

- [game/game-data.md](game/game-data.md): the loadable ruleset (`rulesets/default/v1.json`, read by `Govern.Core/Rulesets`): the boundary between data and Core logic, lifecycle (JSON in the repo, stored per version, pinned per game, served to the client; storing and pinning deferred), the database record, reading it in code (`Ruleset.Parse`, strict JSON, the hash), format conventions, every section with its fields, defaults and examples (setup, map, terrains, units and abilities, cities, texts), validation rules with JSON paths.
- [game/game-logic.md](game/game-logic.md): draft: Core's algorithms as simple steps: creating a game, executing a command (guard, apply, victory, explore), move, attack, found city, set production, end turn, resign, elimination and victory, fog of war and the view version, view previews, randomness and ids, invariants, with the ruleset key for every number.
- [game/design-roadmap.md](game/design-roadmap.md): future game mechanics (G1–G15), what each one is (priorities are in the backlog): the UI design system, a game rules reference, data-driven catalogs (ruleset A done, B deferred), terrain layers, resources and tile yields, rivers and bridges, city territory, stats and production queue, unit roster and combat, builders and buildings with upgrades, science and culture trees with upkeep, civilizations and governors, colonies founded by governors, seasons, ships and river navigation; the decisions taken and open questions (diplomacy, texts and languages, the game logic doc).

## Operations
- [operations/local-development.md](operations/local-development.md): prerequisites, local Postgres, user-secrets, running the Api, tests and the container.
- [operations/configuration.md](operations/configuration.md): every configuration key, its default and where secrets come from.
- [operations/deployment.md](operations/deployment.md): Azure resources, release workflow, one-time setup, known gaps.
- [operations/dns.md](operations/dns.md): Cloudflare records and Azure custom domain binding.
- [operations/branching.md](operations/branching.md): branches, PR flow, release and protection.

## Plans
Priorities live only in the backlog. Plans sit in `active/`, `deferred/` or `done/` by state.
- [plans/backlog.md](plans/backlog.md): **all open work**, prioritised in buckets (Now, Next, Deferred, Ideas) with stable ids: now the UI design system (G1) and a richer ruleset and Core (content slice C1: rules reference G2, terrain G4, units and combat G9); next the game log and animations, player-chosen city names and the scale-out backplane; deferred rulesets in the database (G3b), the other game mechanics (G5–G8, G10–G15), shipping the game and the release follow-ups, simultaneous turns, more players and diplomacy.
- [plans/deferred/ruleset-b-store-and-pin.md](plans/deferred/ruleset-b-store-and-pin.md): deferred plan for Ruleset B: the `rulesets` table and seed migration, pinning games, a cached repository, `GET /api/rulesets/{key}/{version}`, the client fetching the pinned ruleset, lobby setups; its decided and open questions.
- [plans/deferred/follow-ups.md](plans/deferred/follow-ups.md): details of the deferred follow-ups F1–F10: ship the game, least-privilege DB access, monitoring, the sign-up email in the transaction, email verification, invite code rotation, turn timers, simultaneous turns, more than two players.
- [plans/done/ruleset-a-core.md](plans/done/ruleset-a-core.md): Ruleset A (done): Core reads every number from `rulesets/default/v1.json`; the Api and the client embed it.
- [plans/done/game-delivery.md](plans/done/game-delivery.md): the game plans (Core engine, lobby, commands + SSE + lobby lifecycle, client lobby, client board), all done, and the `gamecore` branching flow.
- [plans/done/game-lobby.md](plans/done/game-lobby.md): game plan 2 (done): `Game` aggregate, lobby with invite codes, creator-only start, `jsonb` state, five `/api/games` endpoints.
- [plans/done/template-solution-roadmap.md](plans/done/template-solution-roadmap.md): how the template was built, step by step (done).
- [plans/done/follow-ups.md](plans/done/follow-ups.md): the first backlog (done; open items moved on, now deferred follow-ups).
- [plans/done/follow-ups-delivery.md](plans/done/follow-ups-delivery.md): delivery plans for the first backlog (done); its Entra and monitoring sections are the detailed design for F2 and F3.
- [plans/done/session-and-email-verification.md](plans/done/session-and-email-verification.md): session lifecycle (done); its sign-up email and email verification sections are the detailed design for F4 and F5.
