---
paths:
  - "tests/Govern.UnitTests/**"
---

# Unit tests

**Scope:** unit tests cover **`Govern.Utils` only**, since it is framework-agnostic and pure. Everything else (handlers, persistence, endpoints, auth) is covered by integration tests against a real Postgres. Don't write unit tests with mocked `DbContext`s or handlers.

## Stack

- xUnit v3 on Microsoft Testing Platform (`global.json` → `test.runner`). `Xunit` and `Shouldly` are global usings.
- **Assertions:** Shouldly (`result.ShouldBe(…)`, `Should.Throw<T>(…)`, `await Should.ThrowAsync<T>(…)`). Don't use `Assert.*`.
- **Mocks:** avoid them. Use real implementations or tiny hand-written fakes. Mock only boundaries Utils doesn't own (NSubstitute, added on demand).
- Pass `TestContext.Current.CancellationToken` to anything that takes a `CancellationToken` (analyzer xUnit1051 fails the build otherwise).
- For time, use `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) and never real delays.

## Layout and naming

- The folder and namespace mirror Utils: `src/Govern.Utils/Mediator/Services/MediatorService.cs` → `tests/Govern.UnitTests/Mediator/Services/MediatorServiceTests.cs`.
- Name the class `<TypeUnderTest>Tests` and each test `Method_Scenario_ExpectedResult`. Underscores are allowed in tests (CA1707 is off for `tests/**`). For example: `SendAsync_HandlerRegistered_ReturnsHandlerResponse`.
- Arrange / Act / Assert blocks separated by blank lines. Use one behaviour per test; several asserts about that behaviour are fine.
- Use `[Theory]` + `[InlineData]` / `TheoryData<…>` for input variations instead of copy-pasted facts.

## What to test

- Public behaviour and contracts, including edge cases: null or empty input, cancellation, exceptions and ordering (pipeline behaviours).
- Not private methods or implementation details. If something is hard to test, the design needs changing, not reflection.

## Run

- `dotnet test --project tests/Govern.UnitTests` while iterating.
- Finish with the full `dotnet test Govern.slnx`, which the test gate requires.
