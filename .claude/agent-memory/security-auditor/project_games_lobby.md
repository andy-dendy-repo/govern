---
name: project-games-lobby
description: Games feature (/api/games lobby + Plan 3a commands) audit facts 2026-09-27 - BOLA model, invite-code secret, rate limits/caps, open storage-growth Medium (resign loop)
metadata:
  type: project
---

Audit 2026-09-27 of feature/game-lobby (Plan 2) and of uncommitted feature/game-commands (Plan 3a, branched from gamecore): no Critical/High in either.

Verified facts (re-check if handlers change):
- BOLA: GetGame and SendGameCommand filter by Players.Any(UserId) in SQL (outsider == unknown id, 404, state never loaded); StartGame loads by id then FindPlayer -> not_found first; ListMyGames filtered by user. Command actor = Game.Execute(userId) -> FindPlayer(userId).PlayerId; commands carry no player field.
- Command checks order: membership 404 -> lobby game.not_started -> expectedVersion game.stale_version -> engine (409 with Core code/message, static strings). Revision concurrency token -> DbUpdateConcurrencyException -> stale_version.
- GameCommandBody.BindAsync: 415 non-JSON, 413 >4 KiB (Content-Length and while reading), JsonException -> BadHttpRequestException 400; ExceptionHandlingMiddleware returns a generic title and logs no payload.
- Pipeline: UseAuthentication -> UseAuthorization -> UseRateLimiter, so per-user policies (`games` 20/min on create/join/start, `game-commands` 120/min) always see the JWT sub; IP fallback is unreachable today. GET /api/games and GET /api/games/{id} have no rate limit (reads).
- Responses carry no user ids (PlayerId = GamePlayer.Id); LoggingPipeline logs only request name/error code.
- InviteCode: 16 CSPRNG bytes, 22-char base64url, body-only. Seed CSPRNG, RngState only in jsonb.
- State size is bounded per game (map 20x14 fixed, one unit per tile, explored <= tiles); ~13 KB JSON at start.

Open after the Plan 3a audit:
- Medium (API4), NOT fully closed although docs/plans/game-lobby.md says "resolved": finished games are uncapped and never deleted; resign is allowed any time, so a two-account create->join->start->resign loop still adds ~10-13 games/min per account pair forever (sign-up only IP-limited, no email verification); Postgres 32 GB autoGrow Disabled. Fix proposed: rolling 24h per-user game quota (count created/joined in last 24h incl. finished) and/or storage alert (follow-ups #5).
- Low: caps are soft; a parallel burst within one rate window can overshoot by up to ~limit-1.
- Low (carried): Version / shared NextId reveal opponent activity, enemy Producing/ProductionProgress/MovementLeft visible (moved to Plan 3b). Event filtering still needed before events are returned (3b SSE).
- Note for the client plan: invite code in URL fragment (#code).

**Why:** Plan 3b (SSE, events) builds on these handlers.
**How to apply:** at the 3b audit check event filtering per viewer, SSE auth/connection limits per user, and whether the storage-growth Medium got a time-based quota or alerting.

Plan 3b SSE audit 2026-09-28 (GET /api/games/{id}/stream, feature/game-live-updates):
- Verified: membership checked before streaming (404) and on every re-read (SQL filter); hub carries only game id, snapshot = GetGameResponse per caller, deduped on (Phase, Players.Count, View.Version) so fog-hidden changes emit nothing; heartbeat timer not reset by hidden changes (no timing leak); closes at token exp (lifetime <= 1h by JwtOptions); header-only token (no OnMessageReceived); subscriptions removed on dispose.
- MEDIUM, open at audit: `game-streams` concurrency cap evicted while streams are open. RateLimiter.CreateChained's IdleDuration is non-null when the fixed window is idle even with concurrency leases held (verified empirically, .NET 10); DefaultPartitionedRateLimiter drops partitions idle >10 s -> fresh cap every ~70 s (~50 streams/account per 15-min token). Fix: OwningChainedRateLimiter.IdleDuration returns null if any inner limiter's is null.
- Every command (even hidden) triggers one full-game read per open watcher (<= 8 per game when the cap works).
