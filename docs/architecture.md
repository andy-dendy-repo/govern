---
title: Govern architecture
tags: [architecture, layers, folders, naming, clean-architecture, architecture-tests]
code: [src, tests/Govern.ArchitectureTests, src/Govern.Client]
updated: 2026-09-27
---

# Govern architecture

## System overview

Govern is an HTTP Web API (`Govern.Api`) with a Blazor WebAssembly client (`Govern.Client`, Radzen components).
- The client calls the API over HTTPS with a JWT bearer token in the `Authorization` header.
- Data lives in PostgreSQL, accessed through EF Core.
- The API runs as a Linux container on Azure Web App for Containers. The client is hosted on Azure Static Web Apps.
- DNS is on Cloudflare, and infrastructure is defined in Bicep.

## Server layers and allowed references

The server side follows Clean Architecture: dependencies point inward, toward the domain.

| Project | Responsibility | May reference |
|---|---|---|
| Govern.Utils | Framework-agnostic building blocks such as the mediator | — |
| Govern.Core | Pure, deterministic game engine (rules, fog of war, JSON format), base class library only; shared with the client ([game-core](features/game-core.md)) | — |
| Govern.Domain | Entities, value objects, domain events | Core, Utils |
| Govern.Application.Common | Abstractions implemented by Infrastructure (DB context, factory, transaction, …), shared models and options | Core, Domain, Utils |
| Govern.Application | Use cases grouped by feature (`[Feature]/Contracts`) | Application.Common, Core, Domain, Utils |
| Govern.Infrastructure | Persistence (EF Core + Npgsql) and external integrations, one folder per concern | Application, Application.Common, Core, Domain, Utils |
| Govern.Api | Composition root, minimal API endpoints, middlewares, filters, health checks | all server projects |
| Govern.Client | Blazor WASM UI | Core only (no server project) |

Additional constraints:
- The domain does not use EF Core or ASP.NET Core.
- Application does not use ASP.NET Core.
- API endpoints (`Api/Features`) use Application contracts only, never Infrastructure or Domain types directly.
- `Govern.Core` depends on the base class library only: no project or package references and no `Microsoft.*` types.

## Request flow

1. An endpoint in `Api/Features/[Feature]/XxxEndpoint.cs` binds the HTTP request to an `XxxRequest`. Each endpoint is a static class with `Map(IEndpointRouteBuilder)`, discovered at startup by `Api/Extensions/EndpointExtensions.cs` (no endpoint interface).
2. The endpoint dispatches the request through the mediator (`Govern.Utils.Mediator`, see [ADR 0001](decisions/0001-mediator.md)).
3. The mediator runs the pipelines in order: `ErrorHandlingPipeline` (unexpected exceptions become a safe 500 error), `LoggingPipeline` (one log entry per request), then `ValidationPipeline` (runs `XxxValidator`, returns 400 field errors).
4. `XxxHandler` executes the use case through `IApplicationDbContextFactory`, one context per operation, and returns `Result<XxxResponse>`: the response, or an `Error` for expected failures.
5. The endpoint maps the result with `ToHttpResult()`: success becomes 200 or 201, an error becomes RFC 9457 ProblemDetails with a stable `code`.
6. On `SaveChangesAsync`, EF Core interceptors stamp `CreatedAt`/`UpdatedAt` and, after the save, publish the entities' domain events to `EventHandlers` (for example, the welcome email after sign-up).

Authentication is JWT bearer with a deny-by-default fallback policy (see [ADR 0002](decisions/0002-auth-tokens.md) and [users feature](features/users.md)).

## Folder templates

Folders are created on demand. The templates list which folders **may** exist; none are required.

- **UF (utility folders):** Helpers, Models, Enums, Options, Exceptions, Extensions, Attributes, Services.
- **AGFT (application general folders):** UF plus Abstractions, EventHandlers and Repositories, with root files `DependencyInjection.cs` and `GlobalUsings.cs`.
- **3DFT (DDD folders):** AGFT plus one folder per feature. Each feature folder follows AGFT and adds `Contracts/`.
- **Core template (game engine):** a fixed set of concept folders (`Engine`, `State`, `Map`, `Units`, `Cities`, `Events`, `Visibility`, `Serialization`) plus a flat `Actions/` folder where each `XxxAction.cs` holds one player action: its `XxxCommand` and the `XxxRule` that applies it. It deliberately avoids UF names: UF suffix rules (e.g. `Services` → `Service`) don't fit engine code.

| Project | Template | Extras |
|---|---|---|
| Application | 3DFT | — |
| Application.Common | AGFT only | — |
| Infrastructure | UF | one folder per concern (each UF), `Background/`, `Persistence/` (Configurations, Interceptors, Migrations, `ApplicationDbContext*.cs`) |
| Domain | UF | Entities, Events, ValueObjects, EntityBase |
| Api | UF | Middlewares, Filters, HealthChecks, `Features/[Feature]/XxxEndpoint.cs` |
| Core | Core template | `Engine/`, `State/`, `Map/`, `Units/`, `Cities/`, `Events/`, `Visibility/`, `Serialization/`, plus flat `Actions/XxxAction.cs` holding exactly `XxxCommand` + `XxxRule`; no root files (see [game-core](features/game-core.md)) |
| Client | CFT (UF) | Layout, Components, Auth, `Features/[Feature]/` with `Pages/XxxPage.razor`, `Components/`, `Clients/XxxClient.cs` (see [client](features/client.md)) |

## Naming rules

- The `Helper` suffix is forbidden. Classes are named by role (Resolver, Mapper, Calculator, …).
- Types carry the suffix of their folder: Options, Exception, Extensions, Attribute, Service, EventHandler, Repository, Middleware, Filter, HealthCheck, Configuration, Interceptor, Event, Endpoint.
- A use case lives in `XxxContract.cs`, which contains only `XxxRequest`, `XxxResponse`, `XxxValidator`, `XxxHandler` and file-scoped helper types.
- Handlers are sealed and implement `IRequestHandler<TRequest, TResponse>`.
- Repositories exist only when a service needs caching. All queries for that data then go through the repository.
- Value objects (`Domain/ValueObjects`) are sealed, immutable records compared by value, such as `Email`. Invalid input can't become a value object: `TryCreate` handles untrusted input and `Create` throws for already-validated input. EF Core stores a single-value object in one column through a value converter in the entity's configuration.

## How the rules are enforced

`tests/Govern.ArchitectureTests` fails the build pipeline when a rule is broken.

| Test class | Checks |
|---|---|
| ProjectReferenceTests | csproj references against the allowed-reference table; every src project is registered; `Govern.UnitTests` references only Core, Domain and Utils; Core has no package references; `Govern.ClientTests` references only Client |
| FolderStructureTests | folders and root files (`.cs` and `.razor`) against the templates (including Core); contract file contents; Core action files hold exactly their command and rule; one endpoint per file |
| LayerDependencyTests | type-level dependencies between layers (ArchUnitNET); Core depends on the base class library only; the client uses `HttpClient` only in typed clients and `Auth`, and JS interop only in `Auth` and `Services` |
| NamingConventionTests | Helper ban, folder suffixes, contract type names, sealed handlers, interface prefix, sealed immutable value objects; client pages, components and typed clients |
| CodeMetricsTests | file, member and parameter size limits; `.razor` file and `@code` block limits |
| ClientSecurityTests | the client's CSP and security headers, self-hosted assets, no raw HTML, no secrets in client config |

The templates and reference table are data in `tests/Govern.ArchitectureTests/Conventions/`. The matching Claude rules are in `.claude/rules/architecture.md`, `.claude/rules/project-map.md` and, for the client, `.claude/rules/blazor-client.md`.
