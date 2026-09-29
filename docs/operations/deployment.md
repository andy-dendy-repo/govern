---
title: Deployment to Azure
tags: [deployment, azure, bicep, github-actions, oidc, ghcr, app-service, static-web-apps, postgres, key-vault, health-checks, host-filtering, purge-protection]
code: [infra/main.bicep, infra/modules, infra/main.bicepparam, .github/workflows/release.yml, .github/workflows/ci.yml, .github/scripts/configure-client.sh, src/Govern.Api/Middlewares/HostAllowListMiddleware.cs, src/Govern.Api/Models/HealthCheckRoutes.cs]
updated: 2026-09-28
---

# Deployment to Azure

## What gets deployed

`infra/main.bicep` (resource group scope) creates:

| Resource | SKU | Notes |
|---|---|---|
| App Service plan (Linux) + Web App for Containers | B1 | Runs `ghcr.io/<owner>/govern-api:<sha>`; system-assigned identity; Health check on `/health/live`; `HostFiltering:AllowedHosts` = the default host plus the `API_ORIGIN` host; HTTPS only, TLS 1.2+; basic-auth (SCM/FTP) publishing disabled |
| PostgreSQL Flexible Server v17 + database `govern` | Burstable B1ms, 32 GB | TLS required and verified by the Api (`Ssl Mode=VerifyFull`); firewall allows Azure services only |
| Key Vault (RBAC) | Standard | Secrets `ConnectionStrings--Govern`, `Jwt--SigningKey`; the Web App identity has "Key Vault Secrets User"; soft delete 7 days with **purge protection** (irreversible: secrets can't be purged early, and a deleted vault's name stays reserved for 7 days) |
| Static Web App | Free | Blazor WASM client |
| Azure Monitor metric alert + action group | Free tier of alert rules | `<app>-<env>-pg-storage` fires when the database's average `storage_percent` is above 80% for 30 minutes (storage is a fixed 32 GB, auto-grow off, and writes stop when it is full). The action group emails `ALERT_EMAIL`; without it the alert still exists but notifies nobody (`infra/modules/alerts.bicep`) |

## Release flow

A merged PR from `stage` into `main` triggers `.github/workflows/release.yml`:
1. CI runs: build, format, all tests, vulnerable packages, Bicep compile.
2. The Api image is built with `dotnet publish -t:PublishContainer` and pushed to GHCR, tagged with the commit SHA and `latest`.
3. The workflow logs in to Azure through OIDC and runs `az deployment group create` with `infra/main.bicepparam`. This updates the infrastructure and points the Web App at the new image.
4. The client is published, configured by `.github/scripts/configure-client.sh`, and uploaded to the Static Web App. The script:
   - writes the Api origin into the CSP `connect-src` and `appsettings.Production.json`
   - writes the import map hash into the CSP `script-src`
   - fails if a placeholder is left
   
   See [client](../features/client.md#configuring-the-client-at-release).
5. The workflow waits until `https://<api>/health/ready` returns Healthy (the database is reachable).

Migrations run when the new container starts (`DatabaseMigrationService`).

## One-time setup

1. **Resource group:** `az group create -n govern-prod -l westeurope`.
2. **Azure identity for GitHub:**
   - Create an app registration (or user-assigned identity) with a federated credential for `repo:<owner>/<repo>:environment:production`.
   - Grant it **Contributor** and **Role Based Access Control Administrator** on the resource group; the second is needed for the Key Vault role assignment. Restrict the latter to that role if your policy requires it.
3. **GitHub environment `production`:**
   - Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `POSTGRES_ADMIN_PASSWORD`, `JWT_SIGNING_KEY` (`openssl rand -base64 48`).
   - Variable: `AZURE_RESOURCE_GROUP`.
   - Optional variable: `API_ORIGIN` (lower-case `https://host`, e.g. `https://api.example.com`, no port or trailing slash; the release validates it before deploying) once the Api has a custom domain. Without it, the client uses `https://<apiHostname>` from the Bicep outputs. Its host is also added to the Api's `HostFiltering:AllowedHosts`, so set it in the same release that starts using the custom domain; until then, requests to that domain get 400.
   - Optional variable: `ALERT_EMAIL`, the address that receives operational alerts (database storage). Set it before the first release; empty deploys the alert without a notification.
   - **Deployment branches:** `main` only. The workflow also refuses to publish or deploy from other refs, but this stops `workflow_dispatch` on any other branch from getting the OIDC identity and secrets.
   - Add required reviewers if releases need approval.
4. **GHCR image access** (choose one):
   - Make the `govern-api` package public.
   - Or create a fine-grained token with `read:packages` and add `REGISTRY_USERNAME` and `REGISTRY_PASSWORD` to the environment. The Web App uses them to pull.
5. **Branch protection** on `main` and `stage`: PR required, CI required (see [branching](branching.md)).
6. **DNS and custom domains:** see [dns](dns.md). Add the client's custom domain to `extraCorsOrigins` in `infra/main.bicepparam`.

## Checking the client headers after a release

- `curl -I https://<static-web-app-host>` shows `Content-Security-Policy` with the real Api origin and a `'sha256-…'` import map hash, plus `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, `Permissions-Policy`, `Strict-Transport-Security` and `Cross-Origin-Opener-Policy`.
- `curl -I https://<static-web-app-host>/.auth/login/github` returns 404, because the built-in Static Web Apps auth is disabled.
- Opening the site with DevTools shows no CSP violations in the console.

## Health checks and host filtering

| Route | Checks | Used by | Limits |
|---|---|---|---|
| `/health/live` | none (the process answers) | App Service Health check (`healthCheckPath`), every minute, on the default host | not rate limited; exempt from host filtering as a safeguard in case a platform probe uses another `Host` |
| `/health/ready` | checks tagged `ready` (PostgreSQL) | the release workflow, diagnostics | `health` rate-limiting policy per client IP (`RateLimiting:Health*`); host filtering applies |

`HostAllowListMiddleware` answers 400 to any other `Host` header. The App Service front end already routes only hosts bound to the site, so this is defence in depth; it also rejects custom domains that are bound but not listed. Startup fails outside Development if the list is empty. The allowed hosts are the default `<web-app>.azurewebsites.net` host and the `API_ORIGIN` host (Bicep `apiCustomHostnames`). The built-in `AllowedHosts` setting stays `*` because it can't exempt the liveness probe.

## Known gaps (follow-ups)

The database role and monitoring gaps are tracked in [next follow-ups](../plans/next-follow-ups.md) (items 2 and 3).

- **Database role:** the Api connects with the Postgres **administrator** login. Create a least-privilege role (owner of the `govern` schema only), store its connection string in Key Vault instead, or switch to Entra ID authentication with the Web App's managed identity.
- **Network:** Postgres and Key Vault use public endpoints with firewall or RBAC. Private endpoints need a VNet and a higher App Service tier.
- **Key Vault references:** they resolve after the role assignment propagates. On the very first deployment, restart the Web App if secrets show as unresolved.
- **Monitoring:** only the database storage alert exists. There is no Log Analytics or Application Insights yet. Add diagnostic settings and alerts on refresh-token reuse (EventId 1001), 401/429 spikes and 5xx responses.
