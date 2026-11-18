---
title: "Plan: F13 scale out beyond one Api instance (Redis SSE backplane, shared limits)"
tags: [plan, server, sse, live-updates, scale-out, backplane, redis, pub-sub, realtime, rate-limiting, sign-in-throttling]
code: [src/Govern.Infrastructure/Realtime, src/Govern.Infrastructure/RateLimiting, src/Govern.Infrastructure/Redis, src/Govern.Infrastructure/Caching/Services/RedisSignInThrottlingService.cs, src/Govern.Api/DependencyInjection.cs, src/Govern.Application.Common/Abstractions/IGameChangeService.cs, docs/decisions/0005-live-updates.md]
updated: 2026-10-15
---

# Plan: F13 scale out beyond one Api instance (Redis SSE backplane, shared limits)

**Status:** done 2026-10-15 (backlog F13). Replaced the 2026-09-30 Postgres `LISTEN/NOTIFY` draft: Andrii chose Redis (2026-10-15) and asked for the per-instance limits to be shared in the same item. Redis stays optional; without it the Api runs as one instance exactly as before. Operations guide: [scale-out](../../operations/scale-out.md).

## Context
Live game updates reach players through an in-process hub: `InMemoryGameChangeService` behind `IGameChangeService`. `GameChangedEventHandler` publishes after each saved change, and every open `GET /api/games/{id}/stream` on **that instance** re-reads the game ([ADR 0005](../../decisions/0005-live-updates.md)). With two or more Api instances, a player whose stream is on another instance never sees the move; simultaneous turns (F9) make that more visible. The rate limits and the sign-in throttle are also per instance, so N instances would allow N times the configured rates. With Redis configured, all three move to Redis.

## Scope
- In:
  - a Redis-backed `IGameChangeService` (publish to Redis + local fan-out, resync after a reconnect);
  - shared rate limits: every `RateLimitingPolicies` policy (auth, health, games, game commands, game streams: open streams and openings) counts in Redis;
  - shared sign-in throttling (`ISignInThrottlingService` in Redis);
  - all of it selected only when `ConnectionStrings:Redis` is set; a Redis readiness check; two-instance integration tests with Testcontainers Redis; optional Bicep wiring; docs.
- Out:
  - Provisioning Redis and running more than one instance: no prod deploy yet. Production will use a free external Redis tier (Andrii, 2026-10-15), so Bicep only passes a connection string through Key Vault when the release has one.
  - Client changes: none needed. A stream is one long request, so no sticky sessions.

## Affected areas
- `Infrastructure/Redis/Options/RedisOptions.cs` (`Redis:KeyPrefix`, default `govern`), `Infrastructure/Redis/Helpers/RedisHealthCheck.cs`.
- `Infrastructure/Realtime/Services/RedisGameChangeService.cs` (new; wraps `InMemoryGameChangeService`, which gains `SignalAll()`).
- `Application.Common/Abstractions/IRateLimiterService.cs` (new: `CreateFixedWindow`, `CreateConcurrency`; package `System.Threading.RateLimiting`), `Infrastructure/RateLimiting/Services/{Memory,Redis}RateLimiterService.cs`, `Infrastructure/RateLimiting/Helpers/Redis{RateLimiter,FixedWindowRateLimiter,ConcurrencyLimiter,Lease}.cs`, `Infrastructure/RateLimiting/Models/RedisLimiterContext.cs`.
- `Api/DependencyInjection.cs`: policies build their partitions through `IRateLimiterService`; `OwningChainedRateLimiter` stays.
- `Application.Common/Abstractions/ISignInThrottlingService.cs` becomes async (`TryBeginAttemptAsync`, `ResetAsync`); `Application/Users/Contracts/SignInContract.cs` awaits it; `Infrastructure/Caching/Services/RedisSignInThrottlingService.cs` (new) next to `MemorySignInThrottlingService`.
- `Infrastructure/DependencyInjection.cs`: `AddSharedState` picks the in-memory or Redis implementations.
- `infra/main.bicep`, `infra/main.bicepparam`, `infra/modules/{keyVault,appService}.bicep`, `.github/workflows/release.yml`: optional `REDIS_CONNECTION_STRING` → Key Vault `ConnectionStrings--Redis` → app setting `ConnectionStrings__Redis`.
- Packages: `StackExchange.Redis` (Infrastructure), `System.Threading.RateLimiting` (Application.Common), `Testcontainers.Redis` (IntegrationTests).

## Steps
1. Infrastructure: `RedisGameChangeService`. `Subscribe` uses the local hub; `Publish` signals local subscriptions, then fire-and-forget publishes `<instanceId>:<gameId>` to `<prefix>:game-changed`; at startup (hosted service) it subscribes once and wakes local watchers for other instances' messages. No game data in Redis.
2. Infrastructure: resync. On any `ConnectionRestored`, call `SignalAll()`, retry a subscription that failed at startup, and resend games whose publish failed (kept as pending; found by the code review: without it, a failed publish on the sender was lost for instances that never disconnected). Streams push only if the view changed.
3. Infrastructure: connection with `AbortOnConnectFail=false` (start without Redis) and `BacklogPolicy.FailFast` (fail at once while disconnected, so fallbacks are immediate). Readiness check reports `Degraded` while Redis is down: the Api still serves, so a load balancer must keep it.
4. Application.Common + Application: `IRateLimiterService`; `ISignInThrottlingService` async, `SignInContract` updated.
5. Infrastructure: shared limits.
   - Fixed window: one Lua script per acquire, `INCRBY` the key `<prefix>:rl:<policy>:<partition>` and set its expiry on the first hit.
   - Open streams: a sorted set per user of lease ids scored by expiry; acquire (Lua) drops expired ids and adds one under the limit; disposing the lease removes it. Lease lifetime: access token lifetime + 1 minute, since a stream ends at the token's expiry.
   - Sign-in throttle: check-then-increment in one script, keyed by the SHA-256 of the normalised email; reset deletes the key.
   - While Redis is unreachable, each limiter and the throttle use the in-process implementation for that key.
6. Infrastructure: DI switch on `ConnectionStrings:Redis`; `RedisOptions` validated on start when Redis is on.
7. Api: policies use `IRateLimiterService`.
8. Infra: optional Redis secret and app setting (one instance stays the default).

## Tests
- Unit: none (Infrastructure isn't unit-tested here).
- Integration (`tests/Govern.IntegrationTests/ScaleOut`, `RedisFixture` + `ScaleOutTestBase` with two hosts on one database and one Redis):
  - `GameChangeBackplaneTests`: a start on A reaches streams on A and B; an end turn on B reaches the opponent on A; a change missed during a disconnect arrives after the reconnect (A without Redis stands for the lost message; fails without `SignalAll`); a reconnect without a change pushes only heartbeats; a change whose publish failed on A (its Redis user may not PUBLISH) reaches B after A reconnects.
  - `SharedRateLimitTests`: a fixed window shared across A and B rejects the request past the limit; open streams are capped across instances and a closed one frees a permit on the other.
  - `SharedSignInThrottlingTests`: failed attempts on A and B add up; 20 parallel attempts across both check only 5 passwords; a success on one instance resets the other.
  - `RedisOutageTests`: with Redis unreachable the instance starts, `/health/ready` is 200 `Degraded`, streams and commands work, and the throttle falls back to the instance; with Redis reachable readiness is `Healthy`. A rate-limit fallback keeps its window after the limiter has been idle for over 10 s (found by the security audit), and a Redis connection string without `ssl=true` stops startup outside Development.
  - Existing tests run without Redis on the in-memory implementations, unchanged.
- Architecture: `LayerDependencyTests.OnlyInfrastructure_UsesRedis`; existing folder and naming rules cover the new folders.

## Docs & rules
- New `docs/operations/scale-out.md`; `docs/operations/configuration.md`, `deployment.md`, `local-development.md`; `docs/features/games.md`, `users.md`; ADR 0005 Consequences; `docs/README.md`.
- `.claude/rules/caching.md` (Redis implementations section), `.claude/rules/integration-tests.md` (`RedisFixture`, `ScaleOut/`).
- Close-out in the same PR: plan to `docs/plans/done/`, backlog row to Recently done, links fixed.

## Verification
`dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` green, including the ScaleOut tests (Docker runs Postgres and Redis containers); `bicep build infra/main.bicep` clean.

## Risks & open questions
- **Free tier command quotas:** every rate-limited request costs one Redis command, plus one per saved change and keep-alive pings. A command-metered free tier (Upstash free: 500K commands a month) would run out in real play; a memory-capped tier without a command quota (Redis Cloud free, 30 MB) fits better. Pick the provider at deploy time and check its current limits.
- **Message volume:** each saved change publishes one message of about 50 bytes to one channel, copied to every instance; an instance without a stream for that game drops it after one lookup. 100 active games with a change every 5 seconds is 20 messages a second, times 3 instances = 60 tiny deliveries a second. Per-game channels cost a subscribe/unsubscribe per stream and a resubscribe after every reconnect; worth it only with many instances and heavy traffic.
- **Concurrency leases after a crash:** a stream permit held by a crashed instance stays taken until its lease expires (one token lifetime plus a minute).
