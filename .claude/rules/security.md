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
- Login and registration are rate-limited (ASP.NET Core rate limiter). Login failures return one generic error that does not reveal whether the email exists.

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
- Errors are ProblemDetails without stack traces, exception messages or SQL in production. Details are logged server-side, not returned.
- OpenAPI and developer endpoints are exposed only in Development.

## Logging and privacy

Follow `logging.md`: no passwords, tokens, keys, connection strings or personal data beyond internal ids.

## Dependencies and infrastructure

- NuGet audit is on. A vulnerable package fails the build (warnings are errors), so don't suppress it; upgrade instead. CI runs `dotnet list package --vulnerable --include-transitive`.
- **Postgres:** SSL required, firewall limited to Azure services and the app, and least-privilege app user (no superuser).
- Use managed identities over keys where Azure supports them. The container runs as non-root. Basic-auth publishing (SCM/FTP) stays disabled.
- **GitHub Actions:**
  - Pin every action to a full commit SHA with the version in a comment; Dependabot bumps them.
  - Deploy jobs run only for `refs/heads/main` and in the `production` environment (deployment branches: main).
  - Permissions are least-privilege per job.
  - Never `pull_request_target` with a checkout of PR code.
