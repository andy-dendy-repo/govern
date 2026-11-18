---
title: "Plan: F14 move the production database to Neon Postgres"
tags: [plan, infra, database, postgres, neon, hosting, cost, bicep, release, key-vault, scale-to-zero]
code: [infra/main.bicep, infra/modules, infra/main.bicepparam, .github/workflows/release.yml]
updated: 2026-09-30
---

# Plan: Move the production database to Neon Postgres

**Status:** planned 2026-09-30 (backlog F14, Next). Not started: nothing is deployed yet, and Andrii decides when it runs (now, or together with F1).

## Context
The planned Azure database (Postgres Flexible Server B1ms with 32 GB) would cost about $16 of the ~$30/month bill. Govern's data is a few MB, and the game is idle most of the day. Neon's free plan (0.5 GB, 100 compute-hours a month, compute suspends after 5 idle minutes) runs the same Postgres 17 for $0. The app code, EF model, migrations and tests stay as they are. Only the production infrastructure, release workflow and docs change. The Api stays on App Service B1 (moving the Api to Container Apps would be a separate plan).

## Scope
- In: create the Neon project once (manually), remove the Azure Postgres server and its storage alert from Bicep, pass the Neon connection string through the release into Key Vault, update docs.
- Out: moving the Api off App Service; least-privilege role beyond what Neon gives for free (F2); monitoring (F3); data migration (there is no production data yet); local development and tests (they keep Docker/Testcontainers Postgres).

## Affected areas
- `infra/main.bicep`: drop `module postgres` and `module alerts`, drop `postgresAdministratorLogin`/`postgresAdministratorPassword`, add a `@secure() param databaseConnectionString`, pass it to `keyVault` instead of the built string; update the header comment.
- `infra/modules/postgres.bicep`, `infra/modules/alerts.bicep`: delete (alerts only covers Postgres storage).
- `infra/main.bicepparam`: `param databaseConnectionString = readEnvironmentVariable('DATABASE_CONNECTION_STRING')` instead of `postgresAdministratorPassword`.
- `.github/workflows/release.yml` ("Deploy infrastructure and Api image"): env `DATABASE_CONNECTION_STRING: ${{ secrets.DATABASE_CONNECTION_STRING }}` instead of `POSTGRES_ADMIN_PASSWORD`; drop `ALERT_EMAIL` and its warning.
- Unchanged and reused: `infra/modules/keyVault.bicep` (already takes a secure `connectionString`), `appService.bicep` (Key Vault reference `ConnectionStrings__Govern`), `DatabaseMigrationService` (creates the schema on first start), `/health/ready` (the release waits on it).
- Docs: `docs/operations/deployment.md`, `docs/operations/configuration.md`, `docs/architecture.md` (hosting line, if it names Azure Postgres), `CLAUDE.md` hosting row, `docs/plans/deferred/follow-ups.md` (F2, F3 wording).

## Steps
1. Neon (manual, once, by Andrii):
   - Sign up, create project `govern`, Postgres 17, region **AWS Europe Central (Frankfurt)**, closest to Azure West Europe. Keep scale to zero at 5 minutes (the free plan default).
   - Create database `govern`. Create a role `govern_app` that owns it, so the Api doesn't use `neondb_owner` (a free partial step toward F2).
   - Copy the **direct** (non-pooler) host, so EF migrations and Npgsql prepared statements work.
   - Connection string: `Host=ep-….eu-central-1.aws.neon.tech;Database=govern;Username=govern_app;Password=…;Ssl Mode=VerifyFull;Connection Idle Lifetime=60`. The idle lifetime makes Npgsql drop pooled connections before Neon suspends, so a request after idle doesn't get a dead connection.
   - GitHub environment `production`: add secret `DATABASE_CONNECTION_STRING`, delete `POSTGRES_ADMIN_PASSWORD` and the `ALERT_EMAIL` variable.
2. Infrastructure: the Bicep changes above; `az bicep build --file infra/main.bicep` compiles.
3. Release workflow: the env changes above.
4. Docs: deployment table (Neon row replaces the Postgres and alert rows), one-time setup (Neon step and secret), known gaps (backups, storage limit, public endpoint), configuration table (where `ConnectionStrings:Govern` comes from).

No Domain, Application, Api or Client changes. No migration.

## Tests
- Unit, integration, client: none. Code is unchanged; the existing suite must stay green.
- Architecture: none; existing rules cover it.
- Manual: before merging, run the Api locally against the Neon branch (`dotnet user-secrets set "ConnectionStrings:Govern" "<neon string>"`, `dotnet run --project src/Govern.Api`). Migrations apply, `/health/ready` is Healthy, and after 6+ idle minutes the next request still succeeds (Neon wake-up).

## Docs & rules
- The docs listed above; the backlog close-out in the same PR.
- `CLAUDE.md` Hosting row: "Neon Postgres (free plan)" instead of "cheapest Azure PostgreSQL Flexible Server tier". The `security.md` rule only changes if it names Azure Postgres.
- Run the `security-auditor` agent (infra and CI change).

## Verification
- `dotnet build Govern.slnx`, `dotnet test Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`: all green (unchanged code).
- `az bicep build --file infra/main.bicep`: compiles, no `postgres`/`alerts` modules.
- The manual Neon run above.
- First release: the workflow's `/health/ready` wait passes; Azure shows no Postgres server in the resource group.

## Risks & open questions
- **Backups:** Neon free keeps 6 hours of restore history, where Azure kept 7 days. Acceptable before launch; before F1, add a nightly `pg_dump` (GitHub Action to a private artifact or storage) or move to a paid plan.
- **Storage limit:** 0.5 GB fits roughly tens of thousands of games at ~13 KB each, but writes fail when it's full, and there's no Azure storage alert anymore. Neon shows usage in its console; a real alert belongs to F3.
- **Public endpoint:** Neon accepts connections from any IP (IP allow-lists are paid). This is similar to today's "allow all Azure services" rule. Protection is the password plus `VerifyFull` TLS.
- **Free-hours budget:** the hourly lobby sweep wakes the database 24 times a day (~2 h of 0.25 CU compute a day, ~15 of 100 CU-hours a month). Fine as is. Optionally set `LobbyCleanup__Interval=06:00:00` in App Service settings.
- **Cold query:** the first query after 5 idle minutes waits about 0.5 s for Neon to wake. Npgsql's default 15 s timeout covers it.
- **Scale-out (F13, done):** doesn't affect this move. The backplane uses Redis, not Postgres `LISTEN/NOTIFY`, so it keeps no idle connection open on Neon and scale to zero still works. Details in the [F13 plan](../done/F13-scale-out-backplane.md).
- **Timing (question for Andrii):** release work (F1 and friends) is deferred, and nothing is deployed yet. Do this now, or keep the plan until F1?
