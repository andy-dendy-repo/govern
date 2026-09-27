---
title: Deployment to Azure
tags: [deployment, azure, bicep, github-actions, oidc, ghcr, app-service, static-web-apps, postgres, key-vault]
code: [infra/main.bicep, infra/modules, infra/main.bicepparam, .github/workflows/release.yml, .github/workflows/ci.yml, .github/scripts/configure-client.sh]
updated: 2026-09-27
---

# Deployment to Azure

## What gets deployed

`infra/main.bicep` (resource group scope) creates:

| Resource | SKU | Notes |
|---|---|---|
| App Service plan (Linux) + Web App for Containers | B1 | Runs `ghcr.io/<owner>/govern-api:<sha>`; system-assigned identity; `/health` health check; HTTPS only, TLS 1.2+; basic-auth (SCM/FTP) publishing disabled |
| PostgreSQL Flexible Server v17 + database `govern` | Burstable B1ms, 32 GB | TLS required and verified by the Api (`Ssl Mode=VerifyFull`); firewall allows Azure services only |
| Key Vault (RBAC) | Standard | Secrets `ConnectionStrings--Govern`, `Jwt--SigningKey`; the Web App identity has "Key Vault Secrets User" |
| Static Web App | Free | Blazor WASM client |

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
5. The workflow waits until `https://<api>/health` returns Healthy.

Migrations run when the new container starts (`DatabaseMigrationService`).

## One-time setup

1. **Resource group:** `az group create -n govern-prod -l westeurope`.
2. **Azure identity for GitHub:**
   - Create an app registration (or user-assigned identity) with a federated credential for `repo:<owner>/<repo>:environment:production`.
   - Grant it **Contributor** and **Role Based Access Control Administrator** on the resource group; the second is needed for the Key Vault role assignment. Restrict the latter to that role if your policy requires it.
3. **GitHub environment `production`:**
   - Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `POSTGRES_ADMIN_PASSWORD`, `JWT_SIGNING_KEY` (`openssl rand -base64 48`).
   - Variable: `AZURE_RESOURCE_GROUP`.
   - Optional variable: `API_ORIGIN` (e.g. `https://api.example.com`) once the Api has a custom domain. Without it, the client uses `https://<apiHostname>` from the Bicep outputs.
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

## Known gaps (follow-ups)

- **Database role:** the Api connects with the Postgres **administrator** login. Create a least-privilege role (owner of the `govern` schema only), store its connection string in Key Vault instead, or switch to Entra ID authentication with the Web App's managed identity.
- **Network:** Postgres and Key Vault use public endpoints with firewall or RBAC. Private endpoints need a VNet and a higher App Service tier.
- **Key Vault references:** they resolve after the role assignment propagates. On the very first deployment, restart the Web App if secrets show as unresolved.
- **Key Vault purge protection** is off (soft delete is 7 days). Enabling it is irreversible; decide before production data exists.
- **Monitoring:** there is no Log Analytics or Application Insights yet. Add diagnostic settings and alerts on refresh-token reuse (EventId 1001), 401/429 spikes and 5xx responses.
- **AllowedHosts** is `*`. Set it to the Api host names once custom domains are bound.
