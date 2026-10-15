---
name: redis-shared-state-checks
description: Govern Redis backplane / shared limiter review checks (F13, 2026-10-15) - publisher-side pub/sub loss, phantom leases on timeout, fallback IdleDuration, black-hole timeouts
metadata:
  type: project
---

F13 review (2026-10-15, branch claude/f13-redis-sse-backplane-*) findings worth re-checking on any Redis-backed state:
- Pub/sub resync only covers the SUBSCRIBER's reconnect. A publish that fails on instance A (A's link down, or RedisTimeoutException) is never seen by B, which never disconnected; streams have no periodic re-read (heartbeat only), so the move shows only at token-expiry reconnect (<=15 min). Look for "pending ids republished on restore" or a broadcast resync.
- Lua-script acquire + RedisTimeoutException fallback: the script may have run server-side -> phantom concurrency lease held for leaseLifetime (token lifetime + 1 min) against a limit of 4 streams.
- A custom RateLimiter in a PartitionedRateLimiter is dropped after 10 s of IdleDuration; if it wraps an in-process fallback, IdleDuration must also honour the fallback's (BCL FixedWindow reports null while permits are consumed), else the outage limit resets every 10 s.
- Non-ReplenishingRateLimiter wrappers get no heartbeat TryReplenish, so their BCL fallback runs its own Timer per partition; RateLimitPartition.Get (vs GetFixedWindowLimiter) also keeps AutoReplenishment=true.
- BacklogPolicy.FailFast only helps when the multiplexer KNOWS it is disconnected; a black-holed link waits SyncTimeout/AsyncTimeout (5 s default) per call, including on rate-limited /health.
- SE.Redis 3.x: ConnectionRestored fires for every physical connect (initial too, and maintenance handoffs); Subscription type is separate unless RESP3 + SharedSubscriptionConnection.
- System.Threading.RateLimiting ships in Microsoft.AspNetCore.App, not NETCore.App, so non-Api projects need the package.

**Why:** Redis is optional and production runs one instance, so these paths are only exercised in outages; tests cover the happy paths.
**How to apply:** when reviewing changes to Infrastructure/RateLimiting, Realtime or Redis-backed services, check these first. Related: [[in-process-state-checks]], [[project-known-limitations]].
