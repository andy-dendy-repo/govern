---
name: project-known-limitations
description: Status of the refresh race, sign-up race and events-after-commit gaps found in the first review (all fixed 2026-09-27) and patterns to keep checking
metadata:
  type: project
---

First review (2026-09-27, feature/template-api) found three gaps. All were fixed in the review-fix commit the same day:
- Refresh race: `RevokedAt.IsConcurrencyToken()` in RefreshTokenConfiguration; RefreshTokenHandler translates `DbUpdateConcurrencyException` into `UserErrors.InvalidRefreshToken`; covered by `Post_SameTokenConcurrently_OnlyOneRotationSucceeds`.
- Sign-up race: `IDbExceptionClassificationService.IsUniqueViolation` returns 409; covered by `Post_SameEmailConcurrently_...`.
- Events after commit: DispatchDomainEventsInterceptor logs handler failures (EventId 2001) and doesn't fail the request (best-effort; outbox is a roadmap follow-up).

**Why:** these are the project's patterns for races and post-commit side effects (now in `.claude/rules/persistence.md`).
**How to apply:** in new code, check that check-then-act on rows uses a concurrency token, that unique inserts catch unique violations, and that irreversible side effects are not raised inside explicit transactions. Don't re-report the fixed items above.

Second review (2026-09-27, feature/session-lifecycle: sign-out, session cap, cleanup): every new token-revocation path is a new check-then-act on `revoked_at`. The sign-in cap eviction was added without a `DbUpdateConcurrencyException` catch (500 on a concurrent sign-in/refresh at the cap), and sign-out presented a token without applying the reuse rule from `auth.md`.
**How to apply:** for any handler that revokes or updates refresh tokens, check (1) the concurrency exception is translated or retried, (2) a presented *rotated* token (`ReplacedByTokenId` set) triggers the theft response on every endpoint that accepts a raw refresh token, not only `/refresh`, (3) one `now` is used across handler, cleanup and issuing.
