---
name: add-use-case
description: >
  Adds one Govern use case end to end: Application contract (Request/Response/Validator/Handler), feature errors,
  Api endpoint, integration tests and docs. Use when the user asks for a new command, query or endpoint
  ("add an endpoint to…", "users should be able to…", "create/update/delete/list X").
---

# Add a use case

Run inside `/develop-feature`, which covers the plan, verification and review. This skill is the recipe for the code. Reference implementation: `src/Govern.Application/Users/Contracts/SignUpContract.cs` + `src/Govern.Api/Features/Users/SignUpEndpoint.cs` + `tests/Govern.IntegrationTests/Features/Users/SignUpEndpointTests.cs`.

Read first: `architecture.md`, `api-endpoints.md`, `error-handling.md`, `mapping.md`, and `persistence.md` if data changes.

## 1. Contract: `src/Govern.Application/[Feature]/Contracts/[UseCase]Contract.cs`
- **`[UseCase]Request`:** `sealed record` implementing `IRequest<[UseCase]Request, [UseCase]Response>`. Document every positional parameter.
- **`[UseCase]Response`:** `sealed record` implementing `IResponse`. If the projection is reused, add `static Expression<Func<TEntity, [UseCase]Response>> Projection` (`mapping.md`).
- **`[UseCase]Validator`:** `sealed class` deriving `AbstractValidator<[UseCase]Request>`, with bounds on every string and collection.
- **`[UseCase]Handler`:** `internal sealed class` implementing `IRequestHandler<,>`:
  - Primary constructor, at most 6 dependencies.
  - `await using var db = await dbContextFactory.CreateDbContextAsync(ct);`
  - Reads use `AsNoTracking().Select(...)`.
  - Writes go through domain methods, then `SaveChangesAsync`.
  - Expected failures are `return XxxErrors.Y;`.
  - Ownership is enforced with `ICurrentUserService.UserId`.
- Anything else in the file is a `file`-scoped type.

## 2. Errors: `src/Govern.Application/[Feature]/Models/[Feature]Errors.cs`
- Add `public static readonly Error X = Error.Conflict("feature.snake_code", "Client-safe message.");`.
- Reuse existing errors; never change existing codes.

## 3. Endpoint: `src/Govern.Api/Features/[Feature]/[UseCase]Endpoint.cs`
Copy the shape of `SignUpEndpoint`: static class, `Map(IEndpointRouteBuilder)`, a named `HandleAsync`.
- **Access:** explicit `.RequireAuthorization()` or `.AllowAnonymous()`. A new anonymous route must be added to the allow-list in `tests/Govern.IntegrationTests/Security/EndpointAuthorizationTests.cs`.
- **Metadata:** `.WithName(nameof(...))`, `.WithTags("[Feature]")`, `.Produces<...>(status)`, `.ProducesValidationProblem()`, `.ProducesProblem(...)` for each error status.
- **Docs:** XML docs with `<response code>` for every status.

## 4. Integration tests: `tests/Govern.IntegrationTests/Features/[Feature]/[UseCase]EndpointTests.cs`
- **Base class:** derive from `ApiTestBase(fixture)`. The database is reset per test.
- **Required cases:**
  - Happy path: status, body and persisted state via `Fixture.OpenDbAsync()`.
  - 400 with the exact invalid fields (`ShouldBeValidationProblemAsync`).
  - 401 without a token when protected.
  - Each domain error with its code (`ShouldBeProblemAsync(status, "feature.code")`).
  - Ownership: another user's resource returns 404.
- **Helpers:** add request helpers to `tests/Govern.IntegrationTests/Support/[Feature]Api.cs` (pattern: `UsersApi`).

## 5. Docs
- Update `docs/features/[feature].md`: the use case, endpoint row (method, route, auth, statuses), error codes and business rules.
- Add the file to `docs/README.md` if it is new.

## 6. Check
- `dotnet build Govern.slnx`.
- `dotnet test --project tests/Govern.ArchitectureTests`: contract shape, endpoint shape, folders, size limits.
- `dotnet test Govern.slnx`.
