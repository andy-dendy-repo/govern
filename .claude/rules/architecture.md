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
| Core | free-form | naming rules still apply; concept folders (`Hexes/`, `Maps/`, `Units/`, `Cities/`, `Players/`, `State/`, `Commands/`, `Events/`, `Rules/`, `Pathfinding/`, `Visibility/`, `Engine/`, `Errors/`, `Serialization/`). Details: `game-core.md` |
| Client | CFT (UF) | `Layout/`, `Components/`, `Auth/`, `Features/[Feature]/` = UF + `Pages/` (`XxxPage.razor`), `Components/`, `Clients/` (`XxxClient.cs`). Root: `App.razor`, `_Imports.razor`, `DependencyInjection.cs`, `GlobalUsings.cs`, `Program.cs`. Details: `blazor-client.md` |

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
| `Events` | end with `Event` (in Domain, also derive from `BaseEvent`) |
| `Commands` (Core) | end with `Command` |
| `Rules` (Core) | end with `Rule` |
| `ValueObjects` (Domain) | be sealed and immutable, and not derive from `BaseEntity` (write them as `sealed record`) |
| `Middlewares` / `Filters` / `HealthChecks` | end with `Middleware` / `Filter` / `HealthCheck` |
| `Configurations` / `Interceptors` | end with `Configuration` / `Interceptor` |
| `Api/Features` | be `static` classes ending with `Endpoint` that expose `Map(IEndpointRouteBuilder)`; exactly one type per file, named after the file |
| `Client/Features/[Feature]/Pages` | be components named `XxxPage` |
| `Client/Layout`, `Client/**/Components` | be Razor components |
| `Client/Features/[Feature]/Clients` | be `sealed` classes ending with `Client` |

Also:
- Interfaces start with `I`.
- Nested types (e.g. a `[LoggerMessage]` `Log` class) and `file` types are exempt from the folder suffix rules.

## Contracts (use cases)

`Application/[Feature]/Contracts/[UseCase]Contract.cs` is one file per use case. The `Contract` suffix is required. The file contains **flat, prefixed** types, and anything else is a `file`-scoped type:

```csharp
namespace Govern.Application.Orders.Contracts;

/// <summary>Creates an order for the current user.</summary>
/// <param name="Title">Order title.</param>
public sealed record CreateOrderRequest(string Title) : IRequest<CreateOrderRequest, CreateOrderResponse>;

/// <summary>Identifier of the created order.</summary>
/// <param name="Id">Order id.</param>
public sealed record CreateOrderResponse(Guid Id) : IResponse;

/// <summary>Validates <see cref="CreateOrderRequest"/>.</summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest> { /* RuleFor(...) */ }

/// <summary>Handles <see cref="CreateOrderRequest"/>.</summary>
internal sealed class CreateOrderHandler(IApplicationDbContextFactory dbContextFactory)
    : IRequestHandler<CreateOrderRequest, CreateOrderResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<CreateOrderResponse>> HandleAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // return OrderErrors.Duplicate;            // expected failure → Error (implicit conversion)
        // return new CreateOrderResponse(order.Id); // success → value (implicit conversion)
    }
}

file sealed record OrderRow(Guid Id, string Title); // e.g. a private EF Core projection
```

Real examples: `src/Govern.Application/Users/Contracts/*.cs`.

- Allowed non-`file` names are exactly `XxxRequest`, `XxxResponse`, `XxxValidator` and `XxxHandler`, where `Xxx` is the file prefix.
- Requests are self-referencing (`IRequest<TRequest, TResponse>`), so the mediator resolves the handler without reflection.
- Handlers are `internal sealed`, implement `IRequestHandler<,>` (`Govern.Utils.Mediator`) and return `Result<TResponse>` (see `error-handling.md`).
- Validators are picked up by the mediator's `ValidationPipeline`; handlers never re-validate input.
- Feature errors live in `[Feature]/Models/XxxErrors.cs` as `static readonly Error` fields.

## Endpoints

`Api/Features/[Feature]/[UseCase]Endpoint.cs` holds one static class per endpoint. There is no endpoint interface: `Api/Extensions/EndpointExtensions.cs` discovers every `static class *Endpoint` in `Govern.Api.Features` by reflection and calls its `Map` method.

```csharp
namespace Govern.Api.Features.Users;

/// <summary><c>POST /api/users/sign-up</c>: creates an account (anonymous, rate limited).</summary>
public static class SignUpEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The route builder.</param>
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/users/sign-up", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicies.Auth)
            .WithName(nameof(SignUpEndpoint))
            .WithTags("Users")
            .Produces<SignUpResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    /// <summary>Creates a user account.</summary>
    /// <response code="201">Account created.</response>
    private static async Task<IResult> HandleAsync(SignUpRequest request, IMediatorService mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.SendAsync(request, cancellationToken);
        return result.ToHttpResult(response => TypedResults.Created("/api/users/me", response));
    }
}
```

- The handler is a named method, not a lambda, so its XML docs reach OpenAPI.
- Every endpoint declares its access explicitly (`RequireAuthorization(policy)` or `AllowAnonymous()`), per `security.md`.
- More endpoint conventions (routes, status codes, OpenAPI metadata): `api-endpoints.md`.

## Repositories

Add a repository only when a service needs cached *queries*. Then **all** queries for that data go through the repository, and the service uses the repository together with the cache. Without caching, handlers use `IApplicationDbContextFactory` directly. Never add a generic repository over EF Core. In-process state that isn't query data (counters, throttling) goes behind an `Application.Common` abstraction instead; see `caching.md`.

## Application.Common

It holds only what is shared across features:
- the abstractions Infrastructure implements (`IApplicationDbContext`, `IApplicationDbContextFactory`, `IApplicationDbContextTransaction`, current user, token issuing, …)
- common models (`AccessToken`, `EmailMessage`, paging); `Result`/`Error` live in `Govern.Utils.Results`
- options

Feature-specific code belongs in `Application/[Feature]`.

## DependencyInjection.cs

Each layer exposes one `AddXxx` extension (C# 14 `extension(IServiceCollection services)` block) in its root `DependencyInjection.cs`: `AddApplication()`, `AddInfrastructure(configuration)`, `AddApi(configuration)`. `Api/Program.cs` composes them. Handlers and validators are registered by assembly scanning (`AddMediator`, `AddValidatorsFromAssembly`), never one by one.
