---
title: Template solution roadmap (one User entity)
tags: [plan, roadmap, template, users, auth, persistence, infra, ci]
code: [src, tests, infra, .github/workflows]
updated: 2026-09-27
---

# Plan: grow the empty template into a working solution with one `User` entity

## Status (2026-09-27)

| Step | State |
|---|---|
| 1 Utils mediator + Result | Done (`feature/template-api`) |
| 2 Result/Error mapping to ProblemDetails | Done |
| 3 Domain (`User`, `RefreshToken`, events) | Done, with minimal properties: email and password only |
| 4 Application.Common abstractions | Done |
| 5 Infrastructure (Postgres, JWT, hashing, mock email) | Done |
| 6 Users use cases | Done: sign-up, sign-in, refresh with rotation, current user |
| 7 Api | Done |
| 8 Integration test infrastructure | Done |
| 9 Blazor client | **Deferred.** The Api came first. The client will store tokens in localStorage ([ADR 0002](../decisions/0002-auth-tokens.md)) |
| 10 Container | Done (chiseled Noble image) |
| 11 Bicep | Done; not yet deployed (see [deployment](../operations/deployment.md)) |
| 12 GitHub workflows | Done; not yet run (no remote yet) |
| 13 Claude rules and agents | Done: mapping, error-handling, persistence, api-endpoints, auth rules, `ef-core-specialist`. Still deferred: `blazor-client.md` (with step 9) and `caching.md` (first cache) |
| 14 Pattern skills | Done: add-use-case, add-entity, add-migration, add-infra-concern, outdated-packages |

Extra work added along the way:
- code size limits (architecture tests + CA1502)
- the mapping rule, with an architecture test banning mapping libraries

### Follow-ups

Open work from the first review and security audit, and the deferred Blazor client: [follow-ups.md](follow-ups.md).

The rest of this document is the original plan, kept for reference.

## Context
The Claude setup (rules, skills, agent, hooks) and the architecture tests are in place, but every layer is still empty. This roadmap builds a minimal end-to-end vertical slice, a user who can register, log in and read their profile, touching every layer, the client, the infrastructure and CI/CD. Later features can then copy real patterns. Run each step separately through `/develop-feature`. Each step is a working, tested increment on its own feature branch, with its PR to `stage`.

## Scope
- In: Mediator completion, Result/Error, User entity, persistence on Postgres, JWT auth, the Users endpoints, integration test infrastructure, Radzen client with login, container, Bicep, GitHub workflows, and pattern skills.
- Out: roles and permissions beyond a single `Role` enum, refresh-token rotation (a candidate follow-up), outbox and background jobs, caching.

## Steps

### 1. Utils: finish the mediator
- Fix `IMediatorService.SendAsync`. It currently returns `ValueTask`, which drops `TResponse`, and it throws `NotImplementedException`. Return `ValueTask<TResponse>`, and consider inferring the handler from DI instead of a generic parameter (decide with the user).
- Pipeline behaviours: validation (FluentValidation), logging/performance, and exception → error result.
- Notifications (`INotification` / `INotificationHandler<T>`) for domain events.
- Registration by assembly scanning (`AddMediator(params Assembly[])`), and actually use `MediatorOptions.UseParentScope`.
- Unit tests in `tests/Govern.UnitTests/Mediator/...`.

### 2. Result / Error
- `Application.Common/Models/{Result, Error}.cs`, carrying the error type (Validation, NotFound, Conflict, Unauthorized, Forbidden).
- `Api/Extensions/ResultExtensions.cs` maps a result to `TypedResults` or ProblemDetails (RFC 9457).

### 3. Domain
- `EntityBase/BaseEntity.cs` (Guid v7 `Id`, domain events), `BaseAuditableEntity.cs` (`Created`, `LastModified` as `DateTimeOffset`), `BaseEvent.cs`.
- `Entities/User.cs` (Email, PasswordHash, DisplayName, Role), `Enums/UserRole.cs`, `Events/UserRegisteredEvent.cs`.
- Arch tests to add: Entities derive from `BaseEntity`; Events derive from `BaseEvent`; Domain has no framework dependencies (already enforced).

### 4. Application.Common
- `Abstractions/IApplicationDbContext.cs` (`DbSet<User> Users`, `SaveChangesAsync`, `BeginTransactionAsync`), `IApplicationDbContextFactory.cs`, `IApplicationDbContextTransaction.cs` (`: IAsyncDisposable`, unlike the PlainCV sample).
- `Abstractions/ICurrentUserService.cs`, `IPasswordHasher.cs`, `IJwtTokenService.cs`.
- `Options/JwtOptions.cs` (issuer, audience, signing key from configuration or Key Vault, never from appsettings in the repo).
- `DependencyInjection.cs`.

### 5. Infrastructure
- `Persistence/ApplicationDbContext.cs`, `ApplicationDbContextFactory.cs`, `ApplicationDbContextTransaction.cs` (Npgsql).
- `Persistence/Interceptors/AuditableEntityInterceptor.cs` (uses `TimeProvider`) and `DispatchDomainEventsInterceptor.cs` (async only; no sync-over-async in `SavedChanges`).
- `Persistence/Configurations/UserConfiguration.cs`, with a unique index on the normalised email.
- `Persistence/Migrations/…_Initial` (generated by `dotnet ef`).
- `Persistence/Services/DatabaseMigrationService.cs` (hosted, applies migrations at startup) and `DataSeedingService.cs` (admin user from configuration).
- `Identity/Services/{JwtTokenService, PasswordHasher, CurrentUserService}.cs`. `PasswordHasher` uses `Microsoft.AspNetCore.Identity.PasswordHasher<T>` or Argon2 (decide).
- `DependencyInjection.cs` (`AddInfrastructure`).

### 6. Application: Users feature
- `Users/Contracts/RegisterUserContract.cs`, `LoginUserContract.cs`, `GetCurrentUserContract.cs`.
- `Users/EventHandlers/UserRegisteredEventHandler.cs`, which logs through `[LoggerMessage]`.
- `DependencyInjection.cs` (scan handlers and validators).

### 7. Api
- Endpoint discovery without an interface (decided): `Extensions/EndpointExtensions.cs` has `MapEndpoints(this IEndpointRouteBuilder)`, which finds every `static class *Endpoint` in `Govern.Api.Features` by reflection and invokes its `Map(IEndpointRouteBuilder)`. The shape is already enforced by `ApiFeatureEndpoints_AreStaticClassesWithMapMethod`.
- Security baseline per `.claude/rules/security.md`: a fallback policy that requires authentication, policy name constants, rate limiting on login and register, CORS allow-list from configuration, HSTS, and ProblemDetails without exception details.
- `Features/Users/RegisterUserEndpoint.cs` (anonymous), `LoginUserEndpoint.cs` (anonymous), `GetCurrentUserEndpoint.cs` (authorised).
- `Middlewares/` global exception handling (or `IExceptionHandler`) plus a correlation-id logging scope. `Filters/` for endpoint filters if needed.
- JWT bearer authentication and authorisation policies; CORS limited to the Static Web App origin; OpenAPI (Development).
- `HealthChecks/`: Postgres check (`AspNetCore.HealthChecks.NpgSql` or `AddDbContextCheck`), `/health` and `/health/ready`.
- Remove the WeatherForecast sample (and its test).

### 8. Integration test infrastructure
- `Fixtures/PostgresFixture.cs` (Testcontainers.PostgreSql, one per assembly), `Fixtures/GovernApiFactory.cs`, Respawn reset.
- `Support/AuthenticatedClient.cs` and test data builders.
- `Features/Users/*EndpointTests.cs` (happy path, 400, 401, 409 duplicate email), plus `HealthChecks/HealthEndpointTests.cs`.
- `Security/EndpointAuthorizationTests.cs` enumerates `EndpointDataSource` and fails on any endpoint without `IAuthorizeData` or `IAllowAnonymous` metadata.

### 9. Client
- Radzen setup (`Radzen.Blazor`, theme, `RadzenComponents`); remove the Bootstrap template pages and the broken `lib/bootstrap` link.
- `JwtAuthenticationStateProvider`, a token store (browser storage), and an `AuthorizationMessageHandler` that adds the `Authorization: Bearer` header to Api calls. The Api base URL comes from configuration.
- Pages: Login, Register, Profile (`GetCurrentUser`), plus the layout with login state.
- Add `.claude/rules/blazor-client.md` (component conventions, Radzen usage, no business logic in components) and a Client folder spec in `Conventions/Templates.cs`.

### 10. Container
- The Api image on an Ubuntu-based (chiseled) .NET runtime, via `dotnet publish /t:PublishContainer` or a Dockerfile, running as non-root. Push to GHCR (the cheapest registry).

### 11. infra/ (Bicep)
- `main.bicep` + modules:
  - Linux App Service plan (B1) + Web App for Containers (pulls from GHCR), with app settings or Key Vault references for secrets.
  - Static Web App (Free).
  - PostgreSQL Flexible Server (Burstable B1ms, smallest storage), firewall limited to Azure services.
  - Log Analytics / App Insights if the budget allows.
- Parameters per environment; custom domains. Cloudflare DNS records are documented in `docs/operations/dns.md`.

### 12. GitHub workflows
- `ci.yml`: on PRs to `stage` and `main`, runs restore, build, format check and `dotnet test Govern.slnx` (Testcontainers on `ubuntu-latest`).
- `release.yml`: on push to `main` (a merged stage → main PR):
  - builds and pushes the image
  - runs `az deployment group create` for the Bicep
  - deploys the Web App image and the Static Web App (Client)
  - leaves migrations to `DatabaseMigrationService` at startup
- Branch protection on `main` and `stage` (PR required, CI required) is documented in `docs/operations/branching.md`.

### 13. Claude rules and agents that arrive with the code
Add each item when its step lands, adapted from dotnet-claude-kit where useful and based on the real Govern code:

| After step | Add | Source idea |
|---|---|---|
| 2 | `.claude/rules/error-handling.md`: Result → ProblemDetails, what to throw vs return | kit `error-handling` |
| 5 | `.claude/rules/persistence.md`: DbContext factory usage, configurations, migrations, query rules | kit `ef-core` |
| 5 | `ef-core-specialist` agent: query and index review using the postgres MCP (`explain_query`, `analyze_query_indexes`) | kit `ef-core-specialist` |
| 7 | `.claude/rules/api-endpoints.md`: TypedResults, route groups, OpenAPI metadata, status codes | kit `minimal-api` |
| 7 | `.claude/rules/auth.md`: JWT options, policies, current user, ownership checks | kit `authentication` |
| First cache | `.claude/rules/caching.md`: HybridCache behind repositories | kit `caching` |
| 9 | `.claude/rules/blazor-client.md`: components, Radzen, services, auth state | kit `templates/blazor-app` |
| any | `outdated-packages` skill: check, upgrade and verify NuGet packages without versions from memory | kit `outdated` / `migrate` |

### 14. Pattern skills
Extract skills from the real code once steps 3–8 exist:
- `add-entity`: entity, configuration, `DbSet`, migration, arch rule.
- `add-use-case`: contract, handler, validator, endpoint, integration tests, docs.
- `add-endpoint`
- `add-migration`
- `add-infra-concern`: folder, abstraction in Application.Common, DI, health check.

## Tests
Each step ships its own tests per `/develop-feature`: unit tests for step 1, integration tests from step 5 on, and arch rules for the new base types (step 3) and the Client folder spec (step 9).

## Docs & rules
- `docs/features/users.md` (steps 6–8), `docs/operations/{deployment,dns,branching,configuration}.md` (steps 10–12), and ADRs for the mediator design, the password hashing choice and the token storage choice in the client.
- `.claude/rules/blazor-client.md` (step 9); Claude rules and agents (step 13), pattern skills (step 14).

## Verification
After each step, `/verify` is green. At the end: register → login → profile works locally (Api + Client + Postgres) and in Azure after a stage → main release, and `/health` reports Postgres as healthy.

## Risks & open questions
- The mediator API shape (step 1) affects every later step; settle it first.
- JWT storage in WASM (localStorage vs memory + refresh) is a security trade-off; decide before step 9.
- Cheapest Postgres tier limits: connection count (use pooling) and burst credits.
