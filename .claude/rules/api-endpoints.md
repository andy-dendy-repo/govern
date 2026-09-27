---
paths:
  - "src/Govern.Api/**"
---

# Api endpoints

## Shape (enforced by architecture tests)

- **File and class:** one file per endpoint, `Api/Features/[Feature]/[UseCase]Endpoint.cs`, containing a `public static class XxxEndpoint`.
- **Map method:** `public static void Map(IEndpointRouteBuilder app)`. It is discovered by `Api/Extensions/EndpointExtensions.MapEndpoints()`; there is no registration list and no endpoint interface.
- **Handler:** a named `private static async Task<IResult> HandleAsync(...)`, never a lambda, so its XML docs (`<response>` codes) reach OpenAPI.
- **Dependencies:** endpoints use Application contracts and the mediator only; no Domain or Infrastructure types.

See `architecture.md` for the full example (`SignUpEndpoint`).

## Conventions

| Topic | Rule |
|---|---|
| Routes | `/api/{feature-plural}/{kebab-action}` for commands (`/api/users/sign-up`), resource paths for reads (`/api/users/me`). Lower-case, kebab-case |
| Verbs | GET reads (no body); POST creates or actions; PUT/PATCH updates; DELETE deletes |
| Body | Bind the Application `XxxRequest` directly; route or query values are merged with `request with { Id = id }` |
| Handler body | Two lines: `var result = await mediator.SendAsync(request, cancellationToken);` then `return result.ToHttpResult(...)` |
| Success status | 200 `Ok` (default), 201 `TypedResults.Created(location, response)` for creation, 204 `NoContent` when there is nothing to return |
| Errors | Never build them by hand; `ToHttpResult()` maps `Error` → ProblemDetails (`error-handling.md`) |
| Auth | Always explicit: `.RequireAuthorization(...)` or `.AllowAnonymous()` (`security.md`, `auth.md`). The fallback policy denies anything undeclared |
| Rate limiting | `.RequireRateLimiting(RateLimitingPolicies.Auth)` on anonymous credential endpoints |
| OpenAPI metadata | `.WithName(nameof(XxxEndpoint))`, `.WithTags("Feature")`, `.Produces<TResponse>(status)`, `.ProducesValidationProblem()`, `.ProducesProblem(status)` for every non-2xx status the use case returns |
| Cancellation | Take `CancellationToken cancellationToken` and pass it to the mediator |

## Cross-cutting pieces

- **Middleware** (`Middlewares/*Middleware.cs`, `IMiddleware`, registered transient) handles request-wide concerns. The current one is `ExceptionHandlingMiddleware`.
- **Endpoint filters** (`Filters/*Filter.cs`, `IEndpointFilter`) handle concerns for specific endpoints. Validation is **not** a filter; the mediator validates.
- **Options** (`Options/*Options.cs`) are bound and validated at startup (`ValidateDataAnnotations().ValidateOnStart()`).
- **Health checks:** `/health` is anonymous. Infrastructure registers the checks (`postgres`); Api `HealthChecks/*HealthCheck.cs` holds Api-specific ones.
- **Pipeline order in `Program.cs`:** exception middleware → status code pages → HSTS (non-Development) → HTTPS redirection → CORS → authentication → authorization → rate limiter → endpoints.

## Testing

Every endpoint has `tests/Govern.IntegrationTests/Features/[Feature]/XxxEndpointTests.cs` covering:
- the happy path
- 400
- 401/403 when protected
- each domain error

`Security/EndpointAuthorizationTests` fails if an endpoint declares no access, or if an anonymous endpoint is not in its allow-list. Update the allow-list deliberately.
