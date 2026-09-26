---
paths:
  - "src/**"
---

# Architecture: folders and naming

Every rule here is enforced by `tests/Govern.ArchitectureTests`. The folder specs live in `Conventions/Templates.cs`, and the naming rules in `NamingConventionTests`. **When a rule changes, change the rule file and the test together.**

**All folders are created on demand.** The templates whitelist what may exist; nothing is required. Don't create empty folders.

## Folder templates

- **UF (utility folders):** `Helpers`, `Models`, `Enums`, `Options`, `Exceptions`, `Extensions`, `Attributes`, `Services`
- **AGFT (application general folders):** UF + `Abstractions`, `EventHandlers`, `Repositories`. Root files: `DependencyInjection.cs`, `GlobalUsings.cs`.
- **3DFT (DDD folders):** AGFT + `[Feature]/` folders (e.g. `Orders/`). Each feature folder is AGFT + `Contracts/`.

| Project | Template | Allowed extras |
|---|---|---|
| Application | 3DFT | — |
| Application.Common | AGFT (inherits, does **not** extend) | — |
| Infrastructure | UF | `[Concern]/` (UF inside, e.g. `Caching/`, `Emailing/`, `Telegram/`), `Background/`, `Persistence/` = UF + `Configurations/`, `Interceptors/`, `Migrations/` + root files `ApplicationDbContext.cs`, `ApplicationDbContextFactory.cs`, `ApplicationDbContextTransaction.cs`. Root: `DependencyInjection.cs`, `GlobalUsings.cs` |
| Domain | UF | `Entities/`, `Events/`, `ValueObjects/`, `EntityBase/` (`BaseEntity.cs`, `BaseAuditableEntity.cs`, `BaseEvent.cs`). Root: `GlobalUsings.cs` |
| Api | UF | `Middlewares/`, `Filters/` (endpoint filters), `HealthChecks/`, `Features/[Feature]/XxxEndpoint.cs`. Root: `DependencyInjection.cs`, `GlobalUsings.cs`, `Program.cs` |
| Utils | free-form | naming rules still apply |

Content inside UF/AGFT folders may be organised in sub-folders freely. `Contracts/` and `Features/[Feature]/` are flat.

## Naming

- **`Helper` suffix is forbidden** (`*Helper`, `*Helpers`), even inside `Helpers/`. Name the class after its role: `Resolver`, `Mapper`, `Calculator`, `Builder`, `Formatter`, `Parser`, `Provider`, `Factory`, `Generator`, `Validator`…

| Types in folder | Must |
|---|---|
| `Options` | end with `Options` |
| `Exceptions` | end with `Exception` |
| `Extensions` | be `static` classes ending with `Extensions` |
| `Attributes` | end with `Attribute` |
| `Services` | end with `Service` |
| `Abstractions` | be interfaces (`I*`) or abstract classes |
| `EventHandlers` | end with `EventHandler` |
| `Repositories` | end with `Repository` |
| `Enums` | be enums |
| `Events` (Domain) | end with `Event` (and derive from `BaseEvent`) |
| `Middlewares` / `Filters` / `HealthChecks` | end with `Middleware` / `Filter` / `HealthCheck` |
| `Configurations` / `Interceptors` | end with `Configuration` / `Interceptor` |
| `Api/Features` | be `static` classes ending with `Endpoint` that expose `Map(IEndpointRouteBuilder)`; exactly one type per file, named after the file |

Also:
- Interfaces start with `I`.
- Nested types (e.g. a `[LoggerMessage]` `Log` class) and `file` types are exempt from the folder suffix rules.

## Contracts (use cases)

`Application/[Feature]/Contracts/[UseCase]Contract.cs` is one file per use case. The `Contract` suffix is required. The file contains **flat, prefixed** types, and anything else is a `file`-scoped type:

```csharp
namespace Govern.Application.Orders.Contracts;

/// <summary>Creates an order for the current user.</summary>
public sealed record CreateOrderRequest(string Title) : IRequest;

/// <summary>Identifier of the created order.</summary>
public sealed record CreateOrderResponse(Guid Id) : IResponse;

/// <summary>Validates <see cref="CreateOrderRequest"/>.</summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest> { /* … */ }

/// <summary>Handles <see cref="CreateOrderRequest"/>.</summary>
public sealed class CreateOrderHandler(IApplicationDbContextFactory dbContextFactory)
    : IRequestHandler<CreateOrderRequest, CreateOrderResponse> { /* … */ }

file sealed record OrderRow(Guid Id, string Title); // e.g. an EF Core projection
```

- Allowed non-`file` names are exactly `XxxRequest`, `XxxResponse`, `XxxValidator` and `XxxHandler`, where `Xxx` is the file prefix.
- Handlers are `sealed` and implement `IRequestHandler<,>` (`Govern.Utils.Mediator`).

## Endpoints

`Api/Features/[Feature]/[UseCase]Endpoint.cs` holds one static class per endpoint. There is no endpoint interface: `Api/Extensions/EndpointExtensions.cs` discovers every `static class *Endpoint` in `Govern.Api.Features` by reflection and calls its `Map` method.

```csharp
namespace Govern.Api.Features.Users;

/// <summary>Registers a new user account.</summary>
public static class RegisterUserEndpoint
{
    /// <summary>Maps <c>POST /api/users</c>.</summary>
    /// <param name="app">The route builder of the users group.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users", HandleAsync).AllowAnonymous().WithName(nameof(RegisterUserEndpoint));

    /// <summary>Creates the user.</summary>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="409">Email already registered.</response>
    private static async Task<IResult> HandleAsync(RegisterUserRequest request, IMediatorService mediator, CancellationToken ct) { /* … */ }
}
```

- The handler is a named method, not a lambda, so its XML docs reach OpenAPI.
- Every endpoint declares its access explicitly (`RequireAuthorization(policy)` or `AllowAnonymous()`), per `security.md`.

## Repositories

Add a repository only when a service needs caching. Then **all** queries for that data go through the repository, and the service uses the repository together with the cache. Without caching, handlers use `IApplicationDbContextFactory` directly. Never add a generic repository over EF Core.

## Application.Common

It holds only what is shared across features:
- the abstractions Infrastructure implements (`IApplicationDbContext`, `IApplicationDbContextFactory`, `IApplicationDbContextTransaction`, current user, token issuing, …)
- common models (Result/Error, paging)
- options

Feature-specific code belongs in `Application/[Feature]`.

## DependencyInjection.cs

Each layer exposes one `AddXxx(this IServiceCollection, IConfiguration)` extension in its root `DependencyInjection.cs`. `Api/Program.cs` composes them. Register handlers and validators by assembly scanning, never one by one.
