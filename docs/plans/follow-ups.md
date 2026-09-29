---
title: Follow-ups backlog (after the template Api)
tags: [plan, backlog, follow-ups, security, auth, operations]
code: [src/Govern.Application/Users, src/Govern.Api, infra, .github/workflows]
updated: 2026-09-29
---

# Follow-ups backlog

**Status: done (closed 2026-09-29).** The two open items, #2 least-privilege database access and #5 monitoring and alerts, moved to [next follow-ups](next-follow-ups.md) (items 2 and 3). This file is kept for history.

Open work found by the first code review and security audit of the template Api (`feature/template-api`, 2026-09-27). Run each item through `/develop-feature`: it gets its own plan in `.claude/rules/planning.md` format, then a branch from `stage` and a PR. When an item is done, remove it here and update the related docs.

Sign-out, refresh token cleanup, the transactional sign-up email and email verification (formerly #1, #4, #6 and #7) are planned in [session-and-email-verification](session-and-email-verification.md). Item numbers are kept so existing references stay valid. #11 (quiet first migration log) was dropped as not needed: the one-off EF Core 20102 error on a brand-new database is accepted.

The remaining items are planned, in delivery order, in [follow-ups-delivery](follow-ups-delivery.md). Done:
- #12 stricter email validation and #3 per-account sign-in limiter: see the email rules and sign-in throttling in [users](../features/users.md).
- #8 host filtering and health check split, and #9 Key Vault purge protection (enabled): see [deployment](../operations/deployment.md#health-checks-and-host-filtering).
- #10 Blazor client: foundation (Radzen, CFT architecture, CSP gate, bUnit) and authentication with the login, register and profile pages, delivered through `gamecore` ahead of game Plan 4: see [client](../features/client.md).

| # | Priority | Item | Size |
|---|---|---|---|
| 2 | High | [Least-privilege database access](#2-least-privilege-database-access) | M |
| 5 | Medium | [Monitoring and alerts](#5-monitoring-and-alerts) | M |

## 2. Least-privilege database access

- **Why:** the Api connects with the PostgreSQL administrator login, and the firewall allows all Azure services. The admin password is the only barrier ([deployment known gaps](../operations/deployment.md#known-gaps-follow-ups)).
- **Approach (preferred):**
  - Microsoft Entra authentication for PostgreSQL with the Web App's managed identity: Npgsql's periodic password provider fetches access tokens.
  - A database role mapped to that identity owns only the `govern` database objects.
  - The connection string holds no password.
- **Alternative:** a dedicated `govern_app` login created by a one-time SQL script, with its connection string in Key Vault.
- **Tests:** the deployment health check; integration tests are unchanged (Testcontainers).

## 5. Monitoring and alerts

- **Why:** refresh-token theft (EventId 1001), brute force (401/429 spikes) and 5xx responses are only visible in container stdout (security audit, Low).
- **Approach:**
  - Log Analytics workspace, plus diagnostic settings for App Service console and HTTP logs and PostgreSQL logs (Bicep).
  - Alerts on EventId 1001, on 5xx rate and on 401/429 spikes.
  - Optionally OpenTelemetry to Application Insights.
- **Done:** a database storage alert (`infra/modules/alerts.bicep`, action group emailing the `ALERT_EMAIL` variable), added with game plan 3c; the log-based alerts above can reuse its action group.
- **Tests:** `az bicep build`; a manual check after deploy.
