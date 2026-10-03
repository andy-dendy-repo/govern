# Project map

Govern is an HTTP Web API with a Blazor WASM client. The server layers follow Clean Architecture, and dependencies point inward. Reference rules are enforced by `tests/Govern.ArchitectureTests` (`Conventions/Layers.cs` + `ProjectReferenceTests` + `LayerDependencyTests`).

## Repository

| Path | Purpose |
|---|---|
| `src/` | Production projects |
| `tests/` | `Govern.UnitTests` (Utils, Core and Domain only), `Govern.IntegrationTests` (everything else on the server, Postgres Testcontainers), `Govern.ClientTests` (Blazor client, bUnit), `Govern.ArchitectureTests` (rules) |
| `docs/` | Knowledge base used for RAG (see `docs.md` rule) |
| `rulesets/` | Game rulesets: `<key>/v<N>.json`, the loadable data Core plays with (format: `docs/game/game-data.md`). A published version is never edited; a change is a new version |
| `maps/` | The map pool: `<key>.json`, hand-made maps a lobby can choose (format: `docs/game/maps.md`), embedded in Application and Client. A running game keeps its own copy, so a file may be edited |
| `infra/` | Bicep (Azure) |
| `tools/` | Offline Node tools, run by hand: `unit-models` bakes the 3D board's units (`wwwroot/models/units`) from the CC0 KayKit pack |
| `.github/workflows/`, `.github/scripts/` | CI (PR → stage/main) and release (stage → main); `configure-client.sh` fills the client's CSP and Api origin at release |

## Projects and allowed references

| Project | Purpose | May reference |
|---|---|---|
| `Govern.Utils` | Framework-agnostic building blocks: `Mediator/` (requests, handlers, pipelines, notifications), `Results/` (`Result<T>`, `Error`). Free-form folders | nothing |
| `Govern.Domain` | Entities, value objects, domain events, `EntityBase/` | Core, Utils |
| `Govern.Application.Common` | Abstractions implemented by Infrastructure (db context, factory, transaction, current user, …) + common models/options | Core, Domain, Utils |
| `Govern.Application` | Use cases: `[Feature]/Contracts/XxxContract.cs` (Request/Response/Validator/Handler) | Application.Common, Core, Domain, Utils |
| `Govern.Infrastructure` | Implementations: `Persistence/` (EF Core + Npgsql), one folder per concern (Caching, Emailing, Telegram, …), `Background/` | Application, Application.Common, Core, Domain, Utils |
| `Govern.Api` | Composition root + minimal API endpoints (`Features/[Feature]/XxxEndpoint.cs`), middlewares, filters, health checks | everything server-side |
| `Govern.Client` | Blazor WASM + Radzen UI, talks to Api over HTTP with a JWT bearer header (see `blazor-client.md`) | Core only (no server project) |
| `Govern.Core` | Pure, deterministic game engine: hex map, units, cities, commands, rules, fog of war, JSON format. Base class library only, shared by server and client (see `game-core.md`) | nothing (no packages either) |

Extra constraints:
- Domain must not use EF Core or ASP.NET Core.
- Application must not use ASP.NET Core.
- Core must not use any `Microsoft.*` type or package (`Core_DependsOnlyOnBaseClassLibrary`, `Core_HasNoPackageReferences`).
- `Api/Features` must not use Infrastructure or Domain types directly. Go through Application contracts.

## Where does it go?

| I'm adding… | Put it in |
|---|---|
| An entity / value object / domain event | `Domain/Entities`, `Domain/ValueObjects`, `Domain/Events` |
| A use case (command or query) | `Application/[Feature]/Contracts/[UseCase]Contract.cs` |
| A reaction to a domain event | `Application/[Feature]/EventHandlers/XxxEventHandler.cs` |
| Feature errors (`Error` constants) | `Application/[Feature]/Models/[Feature]Errors.cs` |
| A service shared by several use cases of one feature | `Application/[Feature]/Services/XxxService.cs` |
| A reusable projection / mapping | a static `Projection` / `From` member on the target response or model (`mapping.md`) |
| A mediator pipeline (cross-cutting for all requests) | `Utils/Mediator/Pipelines/XxxPipeline.cs`, registered in `Application/DependencyInjection.cs` |
| Cached queries for a service | `Application/[Feature]/Repositories/XxxRepository.cs` (only when caching is needed) |
| An interface Infrastructure implements | `Application.Common/Abstractions/IXxx.cs` |
| DB context / factory / transaction abstraction | `Application.Common/Abstractions/IApplicationDbContext*.cs` |
| An EF configuration / interceptor / migration | `Infrastructure/Persistence/{Configurations,Interceptors,Migrations}` |
| Migration runner / data seeding | `Infrastructure/Persistence/Services` |
| An external integration (email, Telegram, cache…) | `Infrastructure/[Concern]/` (UF inside) |
| A background job / outbox processor | `Infrastructure/Background/` |
| A game action (command + the rule that applies it) or game event | `Core/Actions/XxxAction.cs` (`XxxCommand` + `XxxRule`), `Core/Events/XxxEvent.cs` (see `game-core.md`) |
| A game number, list or text (unit or terrain stat, city rule, name) | the ruleset: `rulesets/<key>/vN.json`; a new kind of field also needs its record in `Core/Rulesets` (or `UnitDefinition`/`TerrainDefinition`) and a `RulesetValidator` check (see `game-core.md`, `docs/game/game-data.md`) |
| A hand-made map | `maps/<key>.json`, checked by `PredefinedMapTests` against the default ruleset (see `docs/game/maps.md`) |
| An HTTP endpoint | `Api/Features/[Feature]/[UseCase]Endpoint.cs` (one per file) |
| Request/response pipeline logic | `Api/Middlewares` (global) or `Api/Filters` (endpoint filters) |
| A health check | `Api/HealthChecks/XxxHealthCheck.cs` |
| Options bound from config | `Options/XxxOptions.cs` in the owning project |
| A client page (routable component) | `Client/Features/[Feature]/Pages/[Name]Page.razor` |
| A shared / feature-only client component | `Client/Components/` / `Client/Features/[Feature]/Components/` |
| A typed Api client (the only place `HttpClient` is used) | `Client/Features/[Feature]/Clients/[Feature]Client.cs`, DTOs in `Client/Features/[Feature]/Models` |
| Client auth plumbing (token store, auth state, bearer handler) | `Client/Auth/` |
| A test | see `unit-tests.md`, `integration-tests.md`, `client-tests.md`, `architecture-tests.md` |

New `src/` project? Register it in `tests/Govern.ArchitectureTests/Conventions/Layers.cs` and in this file. `SourceProjects_AreAllRegisteredInLayers` fails otherwise.
