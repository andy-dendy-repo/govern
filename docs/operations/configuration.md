---
title: Api configuration reference
tags: [configuration, appsettings, secrets, key-vault, user-secrets, environment-variables]
code: [src/Govern.Api/appsettings.json, src/Govern.Application.Common/Options/JwtOptions.cs, src/Govern.Infrastructure/Options/DatabaseOptions.cs, src/Govern.Api/Options, infra/modules/appService.bicep]
updated: 2026-09-27
---

# Api configuration reference

Configuration follows the standard ASP.NET Core order: `appsettings.json` → `appsettings.{Environment}.json` → user-secrets (Development only) → environment variables. In Azure, App Service app settings become environment variables; nested keys use `__` (e.g. `Jwt__SigningKey`).

## Settings

| Key | Secret | Default | Where it is set | Purpose |
|---|---|---|---|---|
| `ConnectionStrings:Govern` | **yes** | — (required) | user-secrets locally; Key Vault secret `ConnectionStrings--Govern` in Azure | Npgsql connection string |
| `Jwt:SigningKey` | **yes** | — (required, ≥ 32 chars) | user-secrets locally; Key Vault secret `Jwt--SigningKey` in Azure | HS256 signing key |
| `Jwt:Issuer` | no | `govern-api` | appsettings | Token issuer |
| `Jwt:Audience` | no | `govern-client` | appsettings | Token audience |
| `Jwt:AccessTokenLifetime` | no | `00:15:00` | appsettings | 1 min – 1 h |
| `Jwt:RefreshTokenLifetime` | no | `14.00:00:00` | appsettings | 1 h – 90 days |
| `Database:MigrateOnStartup` | no | `true` | appsettings | Apply EF migrations at startup |
| `Cors:AllowedOrigins` | no | `[]` (Development: local client ports) | appsettings; Bicep adds the Static Web App origin | Exact browser origins; `*` is rejected at startup |
| `RateLimiting:AuthPermitLimit` | no | `10` | appsettings | Requests per IP and window for sign-up, sign-in and refresh |
| `RateLimiting:AuthWindow` | no | `00:01:00` | appsettings | Rate-limit window |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | no | — | Bicep (`true` in Azure) | Trust App Service `X-Forwarded-*` headers (scheme and client IP) |

Options with data annotations (`Jwt`, `RateLimiting`) are validated at startup. A missing or short signing key, or a missing connection string, stops the app with a clear error.

## Rules

- Secrets never go in `appsettings*.json`, `launchSettings.json`, Bicep parameter files or the client (`security.md`).
- A new setting is added to this table in the same change, and to Bicep when it differs in Azure.
