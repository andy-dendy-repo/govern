---
title: Govern knowledge base index
tags: [index, docs]
code: [docs]
updated: 2026-09-28
---

# Govern knowledge base

The project's knowledge base, used for RAG. Every document has front matter (title, tags, code paths, updated date) and covers one topic. The writing rules are in `.claude/rules/docs.md`.

## Architecture
- [architecture.md](architecture.md): layers, allowed references, request flow, folder templates (UF / AGFT / 3DFT), naming rules and how they are enforced.

## Features
- [features/game-core.md](features/game-core.md): the pure game engine in `Govern.Core`: hex map, units and cities, commands and rules, error codes, fog of war (`PlayerView` with row-major tiles and hidden enemy production), view-based previews for the client, the opaque view version, random ids, determinism and the JSON format (schema v2).
- [features/games.md](features/games.md): games and lobby: phases, `/api/games` endpoints and status codes, sending commands (opaque per-player `expectedVersion`, rule-violation codes), live updates over SSE (`/stream`, heartbeat, push on visible change), lobby leave/cancel and 7-day expiry, the list's `activityAt`, error codes, per-user rate limits, caps and the rolling 24-hour quota, invite codes, per-game player ids (no user ids exposed), join/start/command races, `games`/`game_players` tables and the `jsonb` state.
- [features/client.md](features/client.md): Blazor WASM client architecture (CFT feature folders), `Api:BaseUrl` configuration, authentication (token store, refresh ahead of expiry and on 401, cross-tab Web Lock, login/register/profile pages, login redirect), typed clients and `ApiError`, the game lobby (`/games`, `/join#<code>` invite links, live game page with SSE reconnect and backoff), the game board (SVG hex map with pan and zoom, selection and highlights from the Core previews, commands one at a time with stale-version reload, game over), CSP and security headers on the Static Web App, release-time configuration, bUnit and architecture tests.
- [features/users.md](features/users.md): sign-up, sign-in, refresh token rotation and reuse detection, session cap and cleanup, sign-out, current user; endpoints, errors, data model.

## Decisions
- [decisions/0001-mediator.md](decisions/0001-mediator.md): in-house mediator with `Result` responses and error-handling, logging and validation pipelines.
- [decisions/0002-auth-tokens.md](decisions/0002-auth-tokens.md): JWT access tokens, rotating refresh tokens, and client token storage.
- [decisions/0003-no-mapping-libraries.md](decisions/0003-no-mapping-libraries.md): EF projections and static `Projection`/`From` members instead of mapping libraries.
- [decisions/0004-game-engine.md](decisions/0004-game-engine.md): authoritative server with a pure, deterministic, dependency-free game engine; hex map, alternating turns, fog of war.
- [decisions/0005-live-updates.md](decisions/0005-live-updates.md): live game updates over SSE with per-player snapshots, an in-process change hub, an opaque view version and random ids.

## Operations
- [operations/local-development.md](operations/local-development.md): prerequisites, local Postgres, user-secrets, running the Api, tests and the container.
- [operations/configuration.md](operations/configuration.md): every configuration key, its default and where secrets come from.
- [operations/deployment.md](operations/deployment.md): Azure resources, release workflow, one-time setup, known gaps.
- [operations/dns.md](operations/dns.md): Cloudflare records and Azure custom domain binding.
- [operations/branching.md](operations/branching.md): branches, PR flow, release and protection.

## Plans
- [plans/game-design-roadmap.md](plans/game-design-roadmap.md): future game mechanics (G1–G15): the UI design system, a game rules reference, data-driven catalogs, terrain layers, resources and tile yields, rivers and bridges, city territory, stats and production queue, unit roster and combat, builders and buildings with upgrades, science and culture trees with upkeep, civilizations and governors, colonies founded by governors, seasons, ships and river navigation; the decisions taken and open questions.
- [plans/next-follow-ups.md](plans/next-follow-ups.md): **all open work**, prioritised: ship the game, least-privilege DB access, monitoring, the sign-up email in the transaction, email verification, `UnitView`, invite code rotation, and the deferred game ideas, simultaneous turns, and more than two players.
- [plans/game-delivery.md](plans/game-delivery.md): the game plans (Core engine, lobby, commands + SSE + lobby lifecycle, client lobby, client board), all done, and the `gamecore` branching flow.
- [plans/game-lobby.md](plans/game-lobby.md): game plan 2 (done): `Game` aggregate, lobby with invite codes, creator-only start, `jsonb` state, five `/api/games` endpoints.
- [plans/template-solution-roadmap.md](plans/template-solution-roadmap.md): how the template was built, step by step (done).
- [plans/follow-ups.md](plans/follow-ups.md): the first backlog (done; open items moved to next follow-ups).
- [plans/follow-ups-delivery.md](plans/follow-ups-delivery.md): delivery plans for the first backlog (done); its Entra and monitoring sections are the detailed design for next follow-ups items 2 and 3.
- [plans/session-and-email-verification.md](plans/session-and-email-verification.md): session lifecycle (done); its sign-up email and email verification sections are the detailed design for next follow-ups items 4 and 5.
