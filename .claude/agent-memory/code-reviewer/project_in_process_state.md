---
name: in-process-state-checks
description: Govern in-memory throttling/caching review checks - MemoryCache SizeLimit compacts LRU (not fail-open), check-then-act across PBKDF2, Reset outside lock
metadata:
  type: project
---

Review 4 (2026-09-27, feature/sign-in-limiter, follow-up #3) found:
- `IsBlocked` -> PBKDF2 verify (~tens of ms) -> `RecordFailure` is check-then-act: concurrent attempts all pass the check, so a distributed attacker gets far more than MaxFailedAttempts guesses. Fix pattern: atomically reserve an attempt under the lock *before* the expensive step, reset on success.
- MemoryCache (10.0.12) at `SizeLimit`: updating an existing key still works (prior size subtracted), but inserting a new key drops it and triggers background compaction of 5% LRU entries (verified with a scratch app). So "when full, new keys are not tracked (fail open)" is false; old counters (e.g. a blocked victim) get evicted. `CacheItemPriority.NeverRemove` gives real fail-open (still expires).
- `Reset` (Remove) outside the lock can be lost against a concurrent RecordFailure.

**Why:** in-process counters are the project's pattern for security limits on the single B1 instance (`.claude/rules/caching.md`).
**How to apply:** for any new in-memory limiter/counter, check atomic reservation vs slow work, what MemoryCache really does when full, cache expiry >= logical window from the latest write, and that docs don't overclaim. Related: [[project-known-limitations]], [[convention-sweep-gaps]] (code-reviewer.md still says "caching only through repositories").

Status (same day, re-review): all three were fixed. The handler now uses atomic `TryBeginAttempt` + locked `Reset`, and entries use `NeverRemove`. I verified empirically that with NeverRemove at capacity, a tracked entry survives, updates still work, and a new insert is dropped until expired entries are compacted. `Retry-After` was deliberately left out of scope. Don't re-report these.
