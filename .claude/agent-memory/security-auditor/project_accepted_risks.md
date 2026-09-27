---
name: project-accepted-risks
description: Govern intentionally anonymous endpoints, documented/accepted security gaps, and findings from the first release audit (2026-09-27)
metadata:
  type: project
---

Intentionally anonymous endpoints (allow-listed in tests/Govern.IntegrationTests/Security/EndpointAuthorizationTests.cs):
POST /api/users/sign-up, /api/users/sign-in, /api/users/refresh, GET /health, /openapi/{documentName}.json (Development only).
Fallback policy = RequireAuthenticatedUser (Api/DependencyInjection.cs).

Accepted/documented risks (don't re-report unless changed or cheaper fix appears):
- Api uses Postgres admin login; Postgres + Key Vault on public endpoints (firewall 0.0.0.0 "Azure services") — docs/operations/deployment.md "Known gaps".
- First-deploy Key Vault reference propagation needs restart — same doc.
- Sign-up 409 reveals registered emails (accepted until email verification) — docs/features/users.md.
- Access tokens stay valid up to 15 min after revocation; tokens in localStorage (CSP on SWA is a stated requirement) — docs/decisions/0002-auth-tokens.md.
- Rate limiting keys on forwarded client IP (ASPNETCORE_FORWARDEDHEADERS_ENABLED=true in Bicep); must be revisited if Cloudflare proxy (orange cloud) is enabled — docs/operations/dns.md.

**Why:** the user documented these as known follow-ups for the first release candidate (feature/template-api, 2026-09-27).
**How to apply:** at each audit, check whether these are still documented and unchanged; re-open only if the exposure grew.

Findings from the 2026-09-27 audit (verdict RELEASE OK):
- Fixed the same day (review-fix commit): release jobs restricted to refs/heads/main (+ environment deployment branches documented); actions pinned to SHAs + Dependabot; Ssl Mode=VerifyFull; SCM/FTP basic auth disabled; concurrent refresh race (RevokedAt concurrency token); users.md enumeration wording corrected.
- Still open, tracked in docs/plans/follow-ups.md: per-account sign-in limiter; monitoring/alerts; AllowedHosts; Key Vault purge protection (owner decision); SWA CSP (gate for client auth).
- Sign-out, sign-out everywhere, a per-user session cap and on-request refresh_tokens cleanup were added on feature/session-lifecycle (2026-09-27). Reuse detection now fires only for rotated tokens (ReplacedByTokenId set).

Session-lifecycle audit (2026-09-27, uncommitted feature/session-lifecycle): no Critical/High.
- Medium (open until fixed): SignOutHandler ignores an already-rotated token (filters RevokedAt == null), so a client signing out with a token an attacker already rotated gets a silent 204 and theft detection never fires; refresh expiry is sliding, so the attacker chain lives on. Fix: treat a rotated token presented to sign-out as reuse.
- Recurring design fact: every non-rotation revocation must pass replacedByTokenId: null; only RotateRefreshToken sets it. Re-check any new revocation path.
- Sign-out/sign-out-everywhere intentionally RequireAuthorization() with no rate-limit policy (no global limiter exists).

Sign-in limiter audit (2026-09-27, feature/sign-in-limiter, follow-up #3): no Critical/High; re-audit after fixes clean.
- Accepted risks (docs/features/users.md "Sign-in throttling"): per-account lockout DoS for as long as the attack continues (refresh not throttled, sessions survive); weak existence signal via owner success resetting the count.
- Fixed same day: TOCTOU (now TryBeginAttempt reserves under a lock before hashing; parallel integration test); cache compaction evicting counters (entries NeverRemove, MaxTrackedAccounts 50k-1M, fail open for new emails only); TooManyRequests logged at Warning; IPv6 /48 caveat documented.
- Verified empirically (Caching.Memory 10.0.12): NeverRemove survives overflow compaction; expired entries (any priority) are purged only by the ExpirationScanFrequency scan (default 1 min), so a full store recovers about 1 min after entries expire.
- Only SignInHandler verifies passwords; any new credential-checking path must use ISignInThrottlingService + the `auth` IP policy.
- Fact: App Service IPv6 inbound is off in Bicep; forwarded-header trust via ASPNETCORE_FORWARDEDHEADERS_ENABLED (ForwardLimit 1, rightmost XFF) is correct.

Blazor client foundation audit (2026-09-27, uncommitted feature/blazor-client-foundation): no Critical/High.
- Accepted (by the implementer, verified in headless Chrome): CSP `style-src-attr 'unsafe-inline'` because Radzen sets inline style attributes; `style-src` itself is `'self'` (no inline `<style>` elements). Narrowed from `style-src 'unsafe-inline'` after the 2026-09-27 audit.
- Facts: CSP lives in src/Govern.Client/wwwroot/staticwebapp.config.json; placeholders __API_ORIGIN__/__IMPORTMAP_HASH__ filled by .github/scripts/configure-client.sh at release (CI dry-runs it). Published index.html has exactly one inline script (the import map, which carries SRI for framework modules). SWA is Free tier, staging disabled.
- Open at that audit: ClientSecurityTests can be bypassed by script-src-elem/-attr or routes[].headers overrides (Medium); HttpClient arch rule skips root namespace Govern.Client; raw-HTML test is text-only (misses AddMarkupContent, RadzenHtmlEditor/Markdown, "window.eval"); "only TokenStore touches localStorage" not enforced yet; SWA pre-configured /.auth providers not blocked.
- Plan 7 must: attach bearer only when request origin == Api:BaseUrl origin; single-flight refresh; clear storage on failed refresh.
