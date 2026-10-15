---
name: project-redis-scale-out
description: F13 Redis backplane/shared rate limits/sign-in throttle audit (2026-10-15, commit 208621b) - findings, facts, what to re-check
metadata:
  type: project
---

F13 (commit 208621b, audited 2026-10-15): optional Redis (ConnectionStrings:Redis) for game-change pub/sub, all rate-limit policies (IRateLimiterService) and the per-account sign-in throttle. No Critical/High.

Facts: owner plans a free external Redis tier (Redis Cloud free, 30 MB, over the internet) - see docs/operations/scale-out.md. Redis errors fall back to per-instance memory (fail open to per-instance, never to "no limit"). Lua uses KEYS/ARGV only (no injection); partitions are JWT sub or IP. Pub/sub carries only instance id + game id. Sign-in key = unsalted SHA-256 of normalised email. /health/ready redis check is Degraded (200), default writer shows only the status word.

Findings reported (open at audit):
- Medium: sign-in throttle reserves a Redis key for ANY syntactically valid email (incl. non-existent), no cap (memory version has MaxTrackedAccounts); on a 30 MB tier ~1k IPv4s can fill it -> eviction of counters (volatile-lru) or OOM -> fallback. Fix: noeviction policy + bound/short keys.
- Medium: RedisFixedWindowRateLimiter.IdleDuration ignores its fallback, so during a Redis outage the partition (and fallback count) is dropped after 10 s idle -> per-IP auth limit ~6x looser; outage can be forced via the memory flood.
- Medium: no TLS enforcement on the Redis connection (ConfigurationOptions.Ssl not validated outside Development).
- Low: unsalted SHA-256 email keys (use HMAC); Warning per fallback (log volume); stale KV secret when REDIS_CONNECTION_STRING is removed; leaked leases after instance crash lock a user's streams <= token lifetime + 1 min; "Degraded" on /health/ready reveals fallback mode.

**Why:** baseline for the next release audit.
**How to apply:** re-check whether these were fixed; re-check if Redis tier/provider, eviction policy, or key scheme changes. Related: [[project-accepted-risks]].
