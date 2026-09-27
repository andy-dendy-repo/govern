---
title: Follow-ups backlog (after the template Api)
tags: [plan, backlog, follow-ups, security, auth, operations]
code: [src/Govern.Application/Users, src/Govern.Api, infra, .github/workflows]
updated: 2026-09-27
---

# Follow-ups backlog

Open work found by the first code review and security audit of the template Api (`feature/template-api`, 2026-09-27), plus the deferred Blazor client. Run each item through `/develop-feature`: it gets its own plan in `.claude/rules/planning.md` format, then a branch from `stage` and a PR. When an item is done, remove it here and update the related docs.

| # | Priority | Item | Size |
|---|---|---|---|
| 1 | High | [Sign-out and sign-out everywhere](#1-sign-out-and-sign-out-everywhere) | S |
| 2 | High | [Least-privilege database access](#2-least-privilege-database-access) | M |
| 3 | Medium | [Per-account sign-in limiter](#3-per-account-sign-in-limiter) | S |
| 4 | Medium | [Email verification](#4-email-verification) | M |
| 5 | Medium | [Monitoring and alerts](#5-monitoring-and-alerts) | M |
| 6 | Low | [Refresh token cleanup](#6-refresh-token-cleanup) | S |
| 7 | Low | [Outbox for domain events](#7-outbox-for-domain-events) | L |
| 8 | Low | [Host filtering and health check split](#8-host-filtering-and-health-check-split) | S |
| 9 | Decide | [Key Vault purge protection](#9-key-vault-purge-protection) | XS |
| 10 | Next feature | [Blazor client](#10-blazor-client) | L |
| 11 | Low | [Quiet first migration log](#11-quiet-first-migration-log) | XS |
| 12 | Low | [Stricter email validation](#12-stricter-email-validation) | XS |

## 1. Sign-out and sign-out everywhere

- **Why:** a refresh token stays valid for up to 14 days. Today it is only revoked by rotation or reuse detection, so a user cannot end a session on a lost or shared device (security audit, Medium).
- **Approach:**
  - `Users/Contracts/SignOutContract.cs`: `SignOutRequest(RefreshToken)` revokes that token for the current user; always return 204, even for unknown tokens.
  - `SignOutEverywhereContract.cs`: `User.RevokeAllRefreshTokens(now)` for the current user.
  - Endpoints `POST /api/users/sign-out` and `POST /api/users/sign-out-everywhere`, both authenticated.
  - Access tokens stay valid until they expire (15 min, [ADR 0002](../decisions/0002-auth-tokens.md)).
- **Tests:**
  - 204, and the token no longer refreshes
  - another user's token is not revoked
  - 401 without an access token

## 2. Least-privilege database access

- **Why:** the Api connects with the PostgreSQL administrator login, and the firewall allows all Azure services. The admin password is the only barrier ([deployment known gaps](../operations/deployment.md#known-gaps-follow-ups)).
- **Approach (preferred):**
  - Microsoft Entra authentication for PostgreSQL with the Web App's managed identity: Npgsql's periodic password provider fetches access tokens.
  - A database role mapped to that identity owns only the `govern` database objects.
  - The connection string holds no password.
- **Alternative:** a dedicated `govern_app` login created by a one-time SQL script, with its connection string in Key Vault.
- **Tests:** the deployment health check; integration tests are unchanged (Testcontainers).

## 3. Per-account sign-in limiter

- **Why:** the `auth` rate limit is per client IP only. A distributed attacker can brute-force one account, and PBKDF2 makes each attempt CPU-heavy on the B1 plan (security audit, Medium).
- **Approach:**
  - Count failed sign-ins per normalised email (for example 5 per 15 min), then return 429 for that account.
  - Store the counters in the database or a cache, and reset them on success.
  - Partition the IP limiter for IPv6 by /64.
- **Tests:**
  - the Nth failure is limited
  - success resets the counter
  - other accounts are unaffected

## 4. Email verification

- **Why:**
  - Sign-up returns 409 for registered emails, which enumerates accounts (accepted risk in [users](../features/users.md)).
  - Anyone can register someone else's address.
- **Approach:**
  - `User.EmailConfirmedAt`, and a single-use confirmation token (stored hashed, like refresh tokens).
  - Sign-up always returns 202. For an existing email, send a "you already have an account" message instead.
  - Sign-in requires a confirmed email.
  - Needs a real email provider (`/add-infra-concern`).
- **Tests:**
  - 202 for both new and existing emails
  - the right email is captured by `FakeEmailService`
  - confirmation happy path, plus expired and reused tokens

## 5. Monitoring and alerts

- **Why:** refresh-token theft (EventId 1001), brute force (401/429 spikes) and 5xx responses are only visible in container stdout (security audit, Low).
- **Approach:**
  - Log Analytics workspace, plus diagnostic settings for App Service console and HTTP logs and PostgreSQL logs (Bicep).
  - Alerts on EventId 1001, on 5xx rate and on 401/429 spikes.
  - Optionally OpenTelemetry to Application Insights.
- **Tests:** `az bicep build`; a manual check after deploy.

## 6. Refresh token cleanup

- **Why:** each sign-in inserts a row and nothing deletes them. The 32 GB database has autoGrow disabled, and a full disk makes PostgreSQL read-only.
- **Approach:**
  - `Infrastructure/Background/RefreshTokenCleanupService` (hosted, daily).
  - `ExecuteDeleteAsync` for tokens expired or revoked more than N days ago.
  - Optionally cap active tokens per user.
- **Tests:** an integration test that runs the cleanup once against seeded rows.

## 7. Outbox for domain events

- **Why:** domain events are delivered best-effort after commit. A failing handler (for example an email provider outage) loses the event ([persistence rule](../../.claude/rules/persistence.md)).
- **Approach:**
  - The interceptor writes events to an `outbox_messages` table in the same save.
  - `Infrastructure/Background/OutboxProcessor` publishes them with retries and marks them processed.
  - Handlers become idempotent.
- **Tests:**
  - an event is persisted with the entity
  - the processor delivers it
  - a failing handler is retried

## 8. Host filtering and health check split

- **Why:**
  - `AllowedHosts` is `*`.
  - `/health` hits the database on every call, anonymously and without throttling.
- **Approach:**
  - Set `AllowedHosts` to the Api host names (via Bicep app settings) once custom domains exist.
  - `/health/live` (no dependencies) for the App Service health check, `/health/ready` (tag `ready`, database) for diagnostics.
- **Tests:** update `HealthEndpointTests` and the endpoint allow-list.

## 9. Key Vault purge protection

- **Why:** someone with Contributor rights could purge the JWT key and the connection string permanently.
- **Decision needed:** `enablePurgeProtection: true` cannot be turned off again, and it keeps the vault name reserved for the retention period after deletion. Decide before production data exists.

## 10. Blazor client

- **Why:** roadmap step 9 was deferred so the Api could come first.
- **Approach:** see [roadmap step 9](template-solution-roadmap.md) and [ADR 0002](../decisions/0002-auth-tokens.md):
  - Radzen UI
  - tokens in localStorage
  - an `AuthorizationMessageHandler`, with one refresh on 401
  - login, register and profile pages
- **Gate:** `staticwebapp.config.json` with a strict CSP (`default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; connect-src 'self' https://<api>; frame-ancestors 'none'`) and security headers **before** tokens are stored.
- **Also:** add `.claude/rules/blazor-client.md` and a Client folder spec in `tests/Govern.ArchitectureTests/Conventions/Templates.cs`.

## 11. Quiet first migration log

- **Why:** on a brand-new database, EF Core logs `fail: Microsoft.EntityFrameworkCore.Database.Command[20102]` for `SELECT ... FROM "__EFMigrationsHistory"` before it creates the history table. It is harmless, but it looks like an error in the first production log and could trip error alerts (#5).
- **Approach:** create the history table explicitly before migrating (`IHistoryRepository.CreateIfNotExistsAsync`), or filter that single event during `DatabaseMigrationService.StartAsync`.
- **Tests:** an integration test on a fresh container asserting no Error-level logs at startup.

## 12. Stricter email validation

- **Why:** `Email.IsValid` (`src/Govern.Domain/ValueObjects/Email.cs`) only checks for exactly one `@` that is neither first nor last. It accepts control characters (CR/LF), internal whitespace, zero-width characters and un-normalised Unicode, so someone can register a lookalike of another user's address. There is no takeover path today (no password reset, and email delivery is mocked), but a real SMTP provider could be exposed to header injection (security audit of the Email value object, Low).
- **Approach:** in `Email.IsValid`, reject `char.IsControl` and whitespace, and apply `Normalize(NormalizationForm.FormC)` before lower-casing (or reject non-ASCII local parts). Do this before password reset or real email delivery, and always send mail to the stored address.
- **Tests:** unit cases in `tests/Govern.UnitTests/Domain/ValueObjects/EmailTests.cs`; sign-up returns 400 for these inputs.
