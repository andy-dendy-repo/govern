---
paths:
  - "src/Govern.Infrastructure/Caching/**"
  - "src/Govern.Application/**/Repositories/**"
---

# Caching and in-process state

The Api runs as **one** Web App instance (B1). In-process state is correct only while that holds; scaling out means moving it to a shared store (Azure Managed Redis) first.

## In-process counters and short-lived state

Example: sign-in throttling (`ISignInThrottlingService` → `Infrastructure/Caching/Services/MemorySignInThrottlingService.cs`).

- The abstraction lives in `Application.Common/Abstractions` and describes the need ("reserve a sign-in attempt for this account"), not the cache. Handlers never touch `IMemoryCache` directly.
- The implementation lives in `Infrastructure/Caching/Services/Memory[Concern]Service.cs`. It is `internal sealed`, registered as a singleton, and disposes what it owns.
- **Own the cache:** create a dedicated `MemoryCache` with `SizeLimit` (from options) and `Size = 1` per entry, instead of sharing the global `IMemoryCache`. An attacker must not be able to grow memory without bound or evict other features' entries.
- **Know what happens when full.** With `SizeLimit` alone, an insert over the limit is dropped **and** starts a compaction that evicts about 5% of entries, least recently used first. For security state such as counters, that lets an attacker wipe a victim's entry. Set `Priority = CacheItemPriority.NeverRemove` so compaction removes only expired entries and new inserts fail open. Document the choice in the feature doc; throttling fails open because the per-IP limiter still applies.
- **Time:** judge windows with the injected `TimeProvider`, so `FakeTimeProvider` tests are deterministic. Cache expiration only frees memory, so it must outlive the logical window: renew it on every write (`AbsoluteExpirationRelativeToNow = window`).
- **Keys:** normalised value objects (`Email.Value`), never raw input. Values are small immutable records or tuples.
- **Atomicity:** serialise read-modify-write with a `Lock`, including removals. When a check guards slow work (password hashing, I/O), **reserve atomically before the work** (`TryBeginAttempt`). Never do check → slow work → record: parallel requests would all pass the check.
- Never log keys that are personal data (emails).
- State is lost on restart or redeploy. Only use this for data where that is acceptable, and say so in the feature doc.

## Cached queries

Repositories exist only for cached queries (`architecture.md`). The first one adds HybridCache behind `Application/[Feature]/Repositories/XxxRepository.cs` and extends this rule. Then invalidate on every write path, and set explicit expirations.
