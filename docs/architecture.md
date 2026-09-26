---
title: Govern architecture
tags: [architecture, layers, folders, naming, clean-architecture, architecture-tests]
code: [src, tests/Govern.ArchitectureTests]
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
| Govern.Domain | Entities, value objects, domain events | Utils |
| Govern.Application.Common | Abstractions implemented by Infrastructure (DB context, factory, transaction, …), shared models and options | Domain, Utils |
| Govern.Application | Use cases grouped by feature (`[Feature]/Contracts`) | Application.Common, Domain, Utils |
| Govern.Infrastructure | Persistence (EF Core + Npgsql) and external integrations, one folder per concern | Application, Application.Common, Domain, Utils |
| Govern.Api | Composition root, minimal API endpoints, middlewares, filters, health checks | all server projects |
| Govern.Client | Blazor WASM UI | no server project |

Additional constraints:
- The domain does not use EF Core or ASP.NET Core.
- Application does not use ASP.NET Core.
- API endpoints (`Api/Features`) use Application contracts only, never Infrastructure or Domain types directly.
- `Govern.Core` is excluded from all rules.

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

| Project | Template | Extras |
|---|---|---|
| Application | 3DFT | — |
| Application.Common | AGFT only | — |
| Infrastructure | UF | one folder per concern (each UF), `Background/`, `Persistence/` (Configurations, Interceptors, Migrations, `ApplicationDbContext*.cs`) |
| Domain | UF | Entities, Events, ValueObjects, EntityBase |
| Api | UF | Middlewares, Filters, HealthChecks, `Features/[Feature]/XxxEndpoint.cs` |

## Naming rules

- The `Helper` suffix is forbidden. Classes are named by role (Resolver, Mapper, Calculator, …).
- Types carry the suffix of their folder: Options, Exception, Extensions, Attribute, Service, EventHandler, Repository, Middleware, Filter, HealthCheck, Configuration, Interceptor, Event, Endpoint.
- A use case lives in `XxxContract.cs`, which contains only `XxxRequest`, `XxxResponse`, `XxxValidator`, `XxxHandler` and file-scoped helper types.
- Handlers are sealed and implement `IRequestHandler<TRequest, TResponse>`.
- Repositories exist only when a service needs caching. All queries for that data then go through the repository.

## How the rules are enforced

`tests/Govern.ArchitectureTests` fails the build pipeline when a rule is broken.

| Test class | Checks |
|---|---|
| ProjectReferenceTests | csproj references against the allowed-reference table; every src project is registered |
| FolderStructureTests | folders and root files against the templates; contract file contents; one endpoint per file |
| LayerDependencyTests | type-level dependencies between layers (ArchUnitNET) |
| NamingConventionTests | Helper ban, folder suffixes, contract type names, sealed handlers, interface prefix |

The templates and reference table are data in `tests/Govern.ArchitectureTests/Conventions/`. The matching Claude rules are in `.claude/rules/architecture.md` and `.claude/rules/project-map.md`.
