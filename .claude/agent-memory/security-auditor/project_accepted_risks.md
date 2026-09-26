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
- Still open, tracked in docs/plans/template-solution-roadmap.md "Follow-ups": sign-out endpoint; per-account sign-in limiter; monitoring/alerts; refresh_tokens cleanup; AllowedHosts; Key Vault purge protection (owner decision); SWA CSP (gate for client auth).
