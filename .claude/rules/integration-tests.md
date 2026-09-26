---
paths:
  - "tests/Govern.IntegrationTests/**"
---

# Integration tests

**Scope:** everything except Utils: endpoints, handlers, validation, persistence, auth, health checks and background work. Tests go **through the HTTP API** using `WebApplicationFactory<Program>` against a **real Postgres in Testcontainers**.
- Never use EF InMemory or SQLite.
- Never mock the database or the mediator.

## Stack

- xUnit v3 + Shouldly, with `TestContext.Current.CancellationToken` passed everywhere.
- **Testcontainers.PostgreSql:** one container per test assembly, shared through an assembly fixture (`[assembly: AssemblyFixture(typeof(PostgresFixture))]`) or a collection fixture. Pin the image tag to the Postgres major version used in Azure.
- `GovernApiFactory : WebApplicationFactory<Program>`:
  - Overrides the connection string with the container's.
  - Replaces external integrations (email, Telegram, …) with test fakes.
  - Replaces `TimeProvider` with `FakeTimeProvider` when time matters.
- **Database reset:** Respawn resets the database between tests (tables only, keep `__EFMigrationsHistory`). Migrations are applied once, at fixture start.
- **Auth:** an `AuthenticatedClient(role)` helper issues a real JWT via the app's token service, or logs in through the endpoint. Don't bypass auth middleware.
- Docker must be running locally. CI runs on `ubuntu-latest`, which has Docker.

## Layout and naming

- `Features/[Feature]/[UseCase]EndpointTests.cs` mirrors `Api/Features`. Infrastructure-only behaviour (interceptors, migrations, seeding) goes in `Persistence/…Tests.cs`.
- Shared plumbing lives in `Fixtures/` (`PostgresFixture`, `GovernApiFactory`) and `Support/` (auth helpers, test data builders).
- Name tests `Method_Scenario_ExpectedResult`, e.g. `Post_ValidRequest_Returns201AndPersistsUser` or `Post_DuplicateEmail_Returns409ProblemDetails`.

## What every endpoint needs

1. The happy path: status code, response body, and persisted state (read back through the DB context or a GET endpoint).
2. Validation failure: 400 with ProblemDetails listing the invalid fields.
3. Auth: 401 without a token, and 403 with the wrong role, when the endpoint is protected.
4. Domain errors the handler returns: 404, 409, and so on.

## Rules

- Tests are independent and order-agnostic. Create the data each test needs with builders, and never rely on another test's data.
- Assert on HTTP contracts (status, ProblemDetails, JSON), not on internal types.
- No `Task.Delay` for synchronisation. Poll with a timeout, or use `FakeTimeProvider`.
- Keep tests fast: reuse the container and factory, and use Respawn instead of re-creating the database.
