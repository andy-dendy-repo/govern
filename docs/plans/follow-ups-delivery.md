---
title: Follow-ups delivery plans
tags: [plan, backlog, follow-ups, security, auth, rate-limiting, health-checks, key-vault, entra, postgres, monitoring, blazor]
code: [src/Govern.Application/Users, src/Govern.Api, src/Govern.Infrastructure, src/Govern.Client, infra, .github/workflows]
updated: 2026-09-29
---

# Follow-ups delivery plans

**Status: done (closed 2026-09-29).** Plans 1–3, 6 and 7 are delivered. Plans 4 (#2) and 5 (#5) moved to [next follow-ups](next-follow-ups.md) (items 2 and 3); their sections below stay as the detailed design for those items.

Delivery plans for the items in [follow-ups](follow-ups.md). Each increment gets its own branch from `stage`, goes through `/develop-feature`, and is merged before the next one starts. When a plan merges, it removes its section here and its row and section in `follow-ups.md`. The last one deletes both files and their index lines.

## Decisions taken

| Item | Decision |
|---|---|
| #9 Key Vault purge protection | Enabled (irreversible, accepted); done |
| #2 Database access | Microsoft Entra authentication with the Web App's system-assigned managed identity |
| #3 Per-account limiter store | In-memory counters (single B1 instance); done |
| #5 Monitoring | One Log Analytics workspace, workspace-based Application Insights, Azure Monitor OpenTelemetry distro exporting Warning+ logs only |

## Delivery order

1. ~~#12 stricter email validation~~ (done).
2. ~~#3 per-account sign-in limiter~~ (done).
3. ~~#8 + #9 health check split, host filtering and Key Vault purge protection~~ (done, see [deployment](../operations/deployment.md#health-checks-and-host-filtering)). The release workflow now waits on `/health/ready`, which Plans 4 and 5 reuse.
4. #2 least-privilege database access through Entra.
5. #5 monitoring and alerts.
6. ~~#10a Blazor client foundation and CSP gate~~ (done, see [client](../features/client.md)).
7. ~~#10b Blazor client authentication and pages~~ (done through `gamecore`, ahead of game Plan 4; see [client](../features/client.md#client-authentication-sign-in-and-token-refresh)).

Plans 2 and 3 of [session-and-email-verification](session-and-email-verification.md) are separate. #10b shipped first, so Plan 3 adds the client's confirm-email page itself.

## Plan 4: Least-privilege database access via Entra managed identity (#2)

### Context
The Api connects as the PostgreSQL administrator, with a password stored in Key Vault. This plan switches the Api to its system-assigned managed identity, authenticated by Microsoft Entra. That identity owns only the `govern` database objects and needs no password. The admin password stays as break-glass only.

### Scope
- In:
  - Entra auth on the server, with the GitHub deploy service principal as Entra admin
  - an idempotent SQL step in `release.yml` that creates the managed-identity role and gives it ownership of the `govern` objects
  - Npgsql periodic token provider
  - the connection string without a password, as a plain app setting
- Out:
  - disabling password auth (a later follow-up once this runs in prod)
  - private networking
  - a separate migration identity

### Affected areas
- **Infra:**
  - `infra/modules/postgres.bicep`: `authConfig { activeDirectoryAuth: 'Enabled', passwordAuth: 'Enabled', tenantId }`, plus the child `administrators` resource (params `postgresEntraAdminObjectId` and `postgresEntraAdminName`, principalType `ServicePrincipal`).
  - `infra/main.bicep:69`: the connection string becomes `Host=…;Database=govern;Username=<webAppName>;Ssl Mode=VerifyFull`.
  - `infra/modules/keyVault.bicep`: drop the `ConnectionStrings--Govern` secret.
  - `infra/modules/appService.bicep`: plain `ConnectionStrings__Govern`, plus `Database__Authentication=ManagedIdentity`.
- **Infra (new):** `infra/sql/grant-app-role.sql`
  - In db `postgres`: `pgaadauth_create_principal('<webAppName>', false, false)` if the role is missing.
  - In db `govern`: `ALTER DATABASE govern OWNER TO`, `ALTER SCHEMA public OWNER TO`, and `REASSIGN OWNED BY govern_admin TO "<webAppName>"`, all scoped to `govern`.
- **Workflow (`release.yml`):**
  1. After `az deployment group create`: add a temporary firewall rule for the runner IP.
  2. Get a token with `az account get-access-token --resource-type oss-rdbms` and run `psql` as the SP.
  3. Remove the firewall rule (`if: always()`).
  4. `az webapp restart`, then wait on `/health/ready`.
- **Infrastructure:**
  - `Options/DatabaseOptions.cs` (+`Authentication`: `Password` | `ManagedIdentity`, default `Password`).
  - `DependencyInjection.cs:48-70` builds an `NpgsqlDataSource` with `NpgsqlDataSourceBuilder`. When `ManagedIdentity`: `UsePeriodicPasswordProvider` with `ManagedIdentityCredential` for scope `https://ossrdbms-aad.database.windows.net/.default`, refresh about 55 min, retry 5 s. Then `UseNpgsql(dataSource)`.
  - Add `Azure.Identity` via `dotnet add package`.

### Steps
1. **Infrastructure:** the options and data-source refactor. Local and tests keep `Password` (no behaviour change).
2. **Infra:** the Bicep changes, the SQL script and the new `main.bicepparam` env vars (`POSTGRES_ENTRA_ADMIN_OBJECT_ID`, `POSTGRES_ENTRA_ADMIN_NAME`).
3. **Workflow:** the grant and restart steps.
4. **Manual, once:** remove the leftover Key Vault secret `ConnectionStrings--Govern`. Incremental deploys don't delete it.

### Tests
- **Integration:** the existing suite (Password mode) must stay green.
- **New:** an options test that `Database:Authentication` has an invalid value → startup fails validation.
- **Architecture:** none.
- **Infra:** `az bicep build`; the grant SQL runs twice without error (idempotent), checked against local Postgres with a fake role name via the `postgres` MCP, read-only first.

### Docs & rules
- New ADR `docs/decisions/NNNN-database-managed-identity.md` (next free number).
- `docs/operations/deployment.md`: resources, release flow, known gaps (remove "Database role"), and the new GitHub secrets.
- `docs/operations/configuration.md` (`Database:Authentication`).
- `docs/operations/local-development.md` (unchanged Password mode).
- `.claude/rules/security.md`: the app never connects as admin.
- Remove #2 from follow-ups.
- Run the `security-auditor` agent (infra and CI change).

### Verification
- Build, all tests and `az bicep build`.
- After release: `/health/ready` is healthy. `SELECT usename FROM pg_stat_activity WHERE datname='govern'` shows `<webAppName>`, not `govern_admin`.

### Risks & open questions
- On the first release, the new image starts before the role exists and fails until the grant step and restart. This is expected and the health wait covers it.
- The workflow assumes `psql` exists on `ubuntu-latest` (it does today). Otherwise the step installs `postgresql-client`.
- Needs the deploy SP's object id as a GitHub secret. The user adds it before release.

---

## Plan 5: Monitoring and alerts (#5)

### Context
Refresh-token theft (EventId 1001), brute force (401/429 spikes) and 5xx responses are visible only in container stdout. This plan adds one Log Analytics workspace with a workspace-based App Insights, the Azure Monitor OTel distro exporting Warning+ logs, diagnostic settings, and email alerts.

### Scope
- In:
  - the workspace (30-day retention, daily cap 1 GB), App Insights, an action group (email)
  - diagnostic settings: Web App `AppServiceHTTPLogs` + `AppServicePlatformLogs`, and PostgreSQL `PostgreSQLLogs`
- Alerts:
  - EventId 1001 (log alert on `AppTraces`, every 15 min)
  - 5xx (metric alert `Http5xx`)
  - 401/429 spike (log alert on `AppServiceHTTPLogs`, unsampled)
- Out:
  - console-log export (it duplicates App Insights)
  - Npgsql tracing
  - dashboards

### Affected areas
- **Infra:**
  - new `infra/modules/monitoring.bicep`
  - `infra/main.bicep` (module and param `alertEmail`, from env `ALERT_EMAIL`)
  - `appService.bicep` (`APPLICATIONINSIGHTS_CONNECTION_STRING`, diagnostic settings)
  - `postgres.bicep` (diagnostic settings)
- **Api:** `DependencyInjection.cs` gets `AddObservability`. It calls `UseAzureMonitor()` only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (so not in local runs or tests), and adds `Logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.Warning)`. Package `Azure.Monitor.OpenTelemetry.AspNetCore` via `dotnet add package`.

### Steps
1. **Api:** `AddObservability` wired in `AddApi`.
2. **Infra:** the monitoring module, diagnostic settings and alerts. The 1001 query filters on the `EventId` custom dimension; check the exporter's property name against the docs, and fall back to the message prefix.
3. **Workflow:** pass `ALERT_EMAIL`.

### Tests
- **Integration:** the existing suite covers startup without a connection string.
- **New:** a test with a dummy `APPLICATIONINSIGHTS_CONNECTION_STRING` via `UseSetting` checks that the app still starts and serves `/health/live`.
- **Architecture:** none.
- **Infra:** `az bicep build`.

### Docs & rules
- New `docs/operations/monitoring.md`: what is collected, the alert table, KQL for each alert, cost notes.
- `deployment.md`: known gaps (remove Monitoring), secrets.
- `configuration.md`.
- `.claude/rules/logging.md`: only Warning+ is exported, and EventIds used by alerts are stable and listed in `monitoring.md`.
- Remove #5 from follow-ups.
- Run `security-auditor`.

### Verification
- Build, all tests and `az bicep build`.
- After release: trigger a reuse (`Govern.Api.http`: refresh with a rotated token) → an alert email within about 20 min. `AppRequests` and `AppTraces` have rows.

### Risks & open questions
- The distro's default sampling may drop some request telemetry. The alerts deliberately use unsampled sources (a metric and HTTP logs), and 1001 comes from logs.
