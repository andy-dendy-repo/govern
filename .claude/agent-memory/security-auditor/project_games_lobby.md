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

Plan 3c lobby lifecycle audit 2026-09-28 (uncommitted feature/game-lobby-lifecycle): no Critical/High/Medium.
- Closes: lobby lock-out Medium (leave/cancel/7-day sweep), storage-growth Medium (30 seats/24h rolling quota in GameSeatLimitService, counting GamePlayer.JoinedAt incl. finished games, + Postgres storage_percent>80% alert), activity-leak Low (MyGameItem.ActivityAt moves only on join/leave/start/turn change/finish).
- Verified: leave/cancel filter membership in SQL (outsider 404), cancel 403 only for seated non-creator, creator-leave 409; both on `games` per-user policy (20/min, now shared by create/join/start/leave/cancel). Quota refunds only happen when the seat row is deleted (leave, cancel, sweep) -> storage-neutral; started games can't be left/cancelled. Sweep: Phase==Lobby && ActivityAt<cutoff repeated in ExecuteDelete (READ COMMITTED re-check), game_players FK cascade at DB level. Interceptor captures entities before save so the cancel's GameChangedEvent fires; SSE streams of leaver/cancelled game end.
- Low, open at audit: (1) residual storage growth now bounded by sign-up rate (auth 10/min/IP shared with sign-in, no email verification): ~5 accounts/min/IP x 15 games/day each ~= 1.4 GB/day/IP; (2) ALERT_EMAIL optional -> alert silently notifies nobody; (3) invite code not rotated after a leave, so a leaver can rejoin indefinitely (creator's only recourse is cancel).
- Fact: games migrations are not on main yet (2026-09-28), so activity_at default 0001-01-01 backfill only affects dev/stage DBs.
- ALERT_EMAIL as a repo variable accepted as reasonable (private repo; email is not a secret); suggested armRoleReceivers as an alternative.

Client lobby audit 2026-09-29 (uncommitted feature/client-lobby, base gamecore): no Critical/High.
- Closed since 3b: the `game-streams` IdleDuration eviction Medium is FIXED (OwningChainedRateLimiter.IdleDuration returns null if any inner limiter's is null).
- Verified: invite code only in `/join#code` fragment + POST body (GamesClient.JoinAsync); JoinPage replaces URL before the POST; GetGame returns InviteCode only to the creator in Lobby; no MarkupString/eval; ClipboardService passes text as an interop argument (no eval); all GamesClient URIs relative (Guid ids) through AuthorizationMessageHandler (bearer only for ApiOptions.BaseUrl origin); JoinPage/GamesPage navigate only to `games/{Guid}` from the Api; reconnect floor 2 s, 429 -> >=15 s, 401/404 stop; Referrer-Policy strict-origin-when-cross-origin.
- MEDIUM, open at audit: JoinPage auto-joins on load from the fragment, so any site can top-level-navigate a signed-in victim to /join#<attacker code> (cross-site forced join); a creator watching SSE starts at once, so the victim can't leave (no resign UI yet); repeat to hit MaxUnfinishedGamesPerUser 20 / 30 seats per 24h. docs/features/client.md says "the player can leave the lobby again" (false vs a hostile creator). Fix: prefill from the fragment, require a Join click.
- Low, open: browser global history keeps the original /join#code (replaceState only fixes session history); login->register click leaves a login?returnUrl=/join#code entry (documented). Backoff jitter only <=1 s -> synchronized reconnect waves after an Api restart.

Client board audit 2026-09-29 (uncommitted feature/client-board, Plan 5b, base origin/gamecore): no Critical/High/Medium.
- Closed since the lobby audit: forced-join Medium FIXED (JoinPage only prefills from the fragment; Join needs a click).
- Verified: SVG uses only constant same-document `<use href="#...">`, no `style` attributes (presentation attrs only), city names (server "City N") as text nodes, classes from enums (ApiJson and GameSerializer reject integer enums); board.js has no sinks (wheel preventDefault + getBoundingClientRect), only via Features/Games/Services/BoardInteropService; client keeps the view only in component memory; commands one at a time (`_sending`), a stale 409 does one GET and never resends (no loop), 429 shown, Resign confirms; late command answers for another game dropped (GamePage id check). Version = SHA-256 of the view & long.MaxValue (fits the >= 0 validator).
- Low, carried: PlayerView.Units is raw Core `Unit`, so enemy MovementLeft is in the JSON (UnitPanel hides it).
