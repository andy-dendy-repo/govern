---
title: Running several Api instances (Redis)
tags: [scale-out, redis, backplane, pub-sub, sse, live-updates, rate-limiting, sign-in-throttling, configuration, health]
code: [src/Govern.Infrastructure/Redis, src/Govern.Infrastructure/Realtime/Services/RedisGameChangeService.cs, src/Govern.Infrastructure/RateLimiting, src/Govern.Infrastructure/Caching/Services/RedisSignInThrottlingService.cs, src/Govern.Infrastructure/DependencyInjection.cs, src/Govern.Api/DependencyInjection.cs, infra/modules/keyVault.bicep, infra/modules/appService.bicep]
updated: 2026-10-15
---

# Running several Api instances (Redis)

The Api keeps three kinds of state in process: the game change hub that wakes live game streams, the rate-limit counters and the per-account sign-in throttle. With one instance that is enough. With two or more, a player streaming from instance B would never hear about a move saved on instance A, and every limit would be N times looser. Setting `ConnectionStrings:Redis` moves all three into one Redis database shared by every instance. Without it, the Api runs exactly as a single instance, which is the default locally, in most tests and in production today.

## What Redis carries

| State | Without Redis | With Redis | Code |
|---|---|---|---|
| Live game updates | `InMemoryGameChangeService` wakes streams on the same instance | `RedisGameChangeService` wakes local streams at once and publishes `<instance>:<gameId>` on the channel `<prefix>:game-changed`; every other instance wakes its own streams of that game | `Infrastructure/Realtime/Services` |
| Rate limits (all `RateLimitingPolicies`) | BCL limiters per instance (`MemoryRateLimiterService`) | Fixed windows: a counter `<prefix>:rl:<hash of policy and partition>` incremented by a Lua script, expiring with the window. Open streams: a sorted set of lease ids scored by expiry. Partitions hold client IP addresses, so keys carry only a 16-byte SHA-256 prefix | `Infrastructure/RateLimiting` behind `IRateLimiterService` |
| Sign-in throttle | `MemorySignInThrottlingService` per instance | A counter `<prefix>:sign-in:<first 16 bytes of the SHA-256 of the normalised email>`; no email is stored in Redis | `Infrastructure/Caching/Services/RedisSignInThrottlingService.cs` |

No game data passes through Redis: a game change message is only an id, and each stream re-reads the game with its own fog of war, as described in [ADR 0005](../decisions/0005-live-updates.md).

## Delivery and resync of game changes

Redis pub/sub delivers a message at most once, and only to instances connected at that moment. Both ends of a lost message are covered:

- **Receiving instance:** when any of its Redis connections is restored, it signals every open stream once. A stream re-reads its game and pushes a snapshot only if the player's view changed, so a reconnect with nothing missed sends nothing to the client.
- **Sending instance:** when publishing a change fails, the game id is kept as pending and published again after the next restored connection or the next successful publish, so the other instances hear about it even if they never disconnected.

A lost message therefore costs a delay and one database read per open stream, never a missed move.

Each saved change is one message of about 50 bytes on one channel, copied to every instance. An instance with no stream for that game drops it after one dictionary lookup. For example, 100 active games with a change every 5 seconds is 20 messages a second; with 3 instances, 60 small deliveries a second. Per-game channels would spare instances the games they don't stream, but cost a subscribe and unsubscribe per stream and a resubscribe of every game after a reconnect, so one channel is used until there are many instances.

## Shared rate limits

The policies in `Api/DependencyInjection.cs` (`auth`, `health`, `games`, `game-commands`, `game-streams`) get their limiters from `IRateLimiterService`, so the configured `RateLimiting:*` numbers hold across all instances together.

- **Fixed windows** start at a partition's first request and end when the Redis key expires (the BCL limiter uses a timer instead; the limits are the same).
- **Open streams** (`game-streams` concurrency): each open stream holds a lease in Redis until it closes. A lease expires after one access token lifetime plus a minute (`Jwt:AccessTokenLifetime`), because a stream ends at the token's expiry anyway. If an instance dies with streams open, their leases free themselves at that expiry.
- The Redis limiters only answer asynchronously: their synchronous attempt always fails, and the ASP.NET Core rate-limiting middleware then asks asynchronously, so each request costs one cheap extra call and one Redis round trip.
- A stream lease whose script timed out is removed again before falling back, since the script may still have run on the server.

## Accepted limitations

- A stream lease released while Redis is briefly unreachable, or held by an instance that crashed, stays counted until it expires (at most one token lifetime plus a minute). Only that user's open-stream cap is affected.
- A stream opened while Redis was unreachable holds no Redis lease, so right after Redis recovers that user may have a few more open streams than the cap, until those streams end.
- While Redis is unreachable, every sign-in attempt and every failed publish logs a Warning, and anonymous `/health/ready` answers `Degraded`.

## When Redis is unreachable

The connection uses `AbortOnConnectFail=false` (the Api starts without Redis and reconnects in the background) and `BacklogPolicy.FailFast` (commands fail at once instead of queueing). Its timeouts are shorter than the library's 5 s defaults unless the connection string sets them (`syncTimeout` and `asyncTimeout` 1 s, `connectTimeout` 2 s): on a link that silently drops packets, each rate-limited request waits up to one timeout until the connection is declared dead and the fallback takes over at once.

| Concern | Behaviour during the outage |
|---|---|
| Live game updates | Streams on the same instance still update; failed publishes are resent and other instances resync when Redis is back |
| Rate limits | Each limiter counts in process for the outage (limits loosen to per instance), logged at Debug (EventId 2310) |
| Sign-in throttle | Counted per instance by `MemorySignInThrottlingService` (Warning, EventId 2320); a success resets both |
| `/health/ready` | The `redis` check reports `Degraded`, so the endpoint still answers 200 and a load balancer keeps the instance |

Publishing and subscribing failures are logged as Warnings (EventIds 2300 and 2301); a restored connection logs EventId 2302.

## Configuration

| Key | Secret | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings:Redis` | **yes** | empty (Redis off) | StackExchange.Redis connection string, e.g. `host:port,password=…,ssl=true`. Outside Development it must include `ssl=true`, or startup fails |
| `Redis:KeyPrefix` | no | `govern` | Prefix of every key and the channel (lowercase letters, digits and `-`, up to 32), so several environments can share one database |

Configure the Redis database with `maxmemory-policy noeviction`. Every key here has an expiry, so an evicting policy (`volatile-lru`, common on free tiers) would let a flood of sign-in attempts for random emails push out real counters, including a victim account's throttle. With `noeviction`, a full database makes the scripts fail, and the Api falls back to per-instance counting instead of forgetting counts.

In Azure, Bicep stores the connection string in Key Vault as `ConnectionStrings--Redis` and adds the `ConnectionStrings__Redis` app setting only when the release has the `REDIS_CONNECTION_STRING` environment secret (`infra/main.bicepparam`). The App Service plan still runs one instance; scaling it out is a separate decision. Removing the secret later removes the app setting but leaves the Key Vault secret: delete it, or rotate the Redis password, when turning Redis off.

## Choosing a Redis provider

Production will use a free external Redis tier (owner, 2026-10-15). Redis needs almost no memory here (counters, leases, one channel), but every rate-limited request costs one command, plus one per saved change and the client's keep-alive pings. A tier metered by commands (Upstash free: 500 K commands a month) would run out in real play; a memory-capped tier without a command quota (Redis Cloud free, 30 MB) fits better. Check the provider's current limits, that it supports pub/sub, Lua scripts (`EVAL`), TLS and the `noeviction` policy, before switching it on.

## Trying two instances locally

Run Redis in Docker (`docker run -d -p 6379:6379 redis:7-alpine`), set `ConnectionStrings:Redis` to `localhost:6379` with user-secrets, and start the Api twice on different ports (`dotnet run --project src/Govern.Api --urls https://localhost:7295`). The integration tests in `tests/Govern.IntegrationTests/ScaleOut` do the same with two in-memory hosts and a Testcontainers Redis.
