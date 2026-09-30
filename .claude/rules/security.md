---
paths:
  - "src/**"
  - "infra/**"
  - ".github/**"
  - "**/appsettings*.json"
---

# Security

Govern is a public API with JWT bearer auth, a public WASM client, and Postgres on Azure. Deny by default and never trust input. The `security-auditor` agent audits against this rule before each stage → main release.

## Secrets

- Never put secrets in code, `appsettings*.json`, `launchSettings.json`, Bicep parameter files, or the Client. This covers connection strings, the JWT signing key, API keys and passwords.
- **Local:** `dotnet user-secrets` (Api). **Azure:** App Service settings with Key Vault references. **GitHub Actions:** repository or environment secrets, and OIDC federated credentials for Azure login instead of publish profiles or stored credentials.
- `Govern.Client` is downloaded by every visitor. Anything in its `wwwroot/appsettings*.json` or code is public: only public URLs and client ids go there.

## Authentication (JWT)

- Validate issuer, audience, lifetime and signing key. Keep `ClockSkew` small (≤ 1 min). The signing key is at least 256 bits and comes from configuration or a secret, never a literal.
- Keep access tokens short-lived. Tokens are sent only in the `Authorization: Bearer` header, never in URLs, query strings or logs.
- Passwords are hashed with a slow, salted algorithm (`PasswordHasher<T>` PBKDF2 or Argon2). They are never logged, returned or stored reversibly.
- Login and registration are rate-limited (ASP.NET Core rate limiter, per IP and IPv6 /64), and failed logins are throttled per account (`ISignInThrottlingService`). Login failures return one generic error that does not reveal whether the email exists.
- Signed-in endpoints that create rows or can be spammed get a per-user rate-limit policy (the JWT `sub`: `games`, `game-commands`) and, where rows accumulate, a per-user cap (e.g. unfinished games) plus a rolling quota when the rows outlive the cap (e.g. 30 game seats per 24 hours, finished games included), so storage can't grow without bound. Database storage has an Azure Monitor alert (`infra/modules/alerts.bicep`). Bodies with game payloads are capped in bytes (`GameCommandBody.MaxBytes`). Long-lived streams (SSE) cap open connections per user (`game-streams`), end at the access token's expiry, and push only data the caller may see.

## Authorization

- **Every endpoint states its access explicitly:** `.RequireAuthorization(PolicyNames.X)` or `.AllowAnonymous()`, directly or through its group.
  - A fallback policy that requires authenticated users is configured, so a forgotten declaration denies access instead of exposing the endpoint.
  - An integration test enumerates `EndpointDataSource` and fails on any endpoint without auth metadata (roadmap step 8).
- Policy names are constants, never string literals scattered around the code.
- **Ownership:** handlers check that the current user may access the requested resource (filter by `CurrentUser.Id`). Never trust ids from the route or body alone.

## Input and data access

- Every request has a Validator. Bound strings, collections and page sizes (max page size enforced).
- Use EF Core LINQ or `FromSql($"…")` (parameterised). Never concatenate SQL strings.
- Never bind request DTOs directly to entities (mass assignment). Map explicitly.
- Validate file uploads (size, content type, extension), and never use client file names as paths.

## HTTP surface

- HTTPS redirection, plus HSTS outside Development.
- **CORS:** allow-list the Static Web App origin(s) from configuration. Never combine `AllowAnyOrigin` with credentials or use `*` in production.
- **Host header:** `HostAllowListMiddleware` allows only `HostFiltering:AllowedHosts` (set by Bicep) and answers 400 otherwise; only `/health/live` is exempt. Never build absolute URLs from `Request.Host`; use configured base URLs.
- Errors are ProblemDetails without stack traces, exception messages or SQL in production. Details are logged server-side, not returned.
- OpenAPI, Swagger UI and developer endpoints are exposed only in Development.
- **Client (Static Web App):** `wwwroot/staticwebapp.config.json` serves a strict CSP (`script-src 'self' 'wasm-unsafe-eval'` + the import map hash, never `'unsafe-inline'`/`'unsafe-eval'` for scripts; inline styles only as attributes; `connect-src` limited to the Api origin; `frame-ancestors 'none'`; built-in SWA auth disabled) and security headers. The client never renders raw HTML (`MarkupString`) and loads no external assets, and only `Auth/TokenStore.cs` touches `localStorage` (where the tokens are). Post-login redirects follow local paths only (`Auth/ReturnUrl.Sanitize`), and every use of the refresh token (refresh and sign-out) runs in `Auth/AuthSession` under a cross-tab lock, so no tab ever presents a rotated refresh token (the Api answers that by revoking every session). The bearer is sent only to the Api origin. Invite codes travel only in a URL fragment (`/join#<code>`, dropped from the URL at once) and in request bodies, never in a path or query; opening an invite link never joins by itself (a click does). Details: `blazor-client.md`; enforced by `ClientSecurityTests`.

## Logging and privacy

Follow `logging.md`: no passwords, tokens, keys, connection strings or personal data beyond internal ids.

## Dependencies and infrastructure

- NuGet audit is on. A vulnerable package fails the build (warnings are errors), so don't suppress it; upgrade instead. CI runs `dotnet list package --vulnerable --include-transitive`.
- **Postgres:** SSL required, firewall limited to Azure services and the app, and least-privilege app user (no superuser).
- **Key Vault:** RBAC, soft delete and purge protection (enabled; irreversible). Never turn soft delete off or recreate the vault under a new name to work around it.
- Use managed identities over keys where Azure supports them. The container runs as non-root. Basic-auth publishing (SCM/FTP) stays disabled.
- **GitHub Actions:**
  - Pin every action to a full commit SHA with the version in a comment; Dependabot bumps them.
  - Deploy jobs run only for `refs/heads/main` and in the `production` environment (deployment branches: main).
  - Permissions are least-privilege per job.
  - Never `pull_request_target` with a checkout of PR code.
