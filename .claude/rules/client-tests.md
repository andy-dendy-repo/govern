---
paths:
  - "tests/Govern.ClientTests/**"
---

# Client tests (bUnit)

**Scope:** `tests/Govern.ClientTests` tests `Govern.Client` only: Razor components, client services, typed Api clients and auth plumbing. It may reference only `Govern.Client` (`ProjectReferenceTests.ClientTests_ReferenceOnlyClient`). The Api contracts the client calls are covered by the Api integration tests, never here.

## Stack

- xUnit v3 on Microsoft Testing Platform, **bUnit** (`BunitContext`, `Render<T>`), Shouldly. `Xunit`, `Shouldly` and `Bunit` are global usings.
- **Assertions:** Shouldly on the rendered DOM (`cut.Find("h1").TextContent.ShouldBe(…)`). Don't use `Assert.*` or brittle full-markup snapshots of Radzen output.
- **Context:** component tests derive from `Support/ClientTestContext`. It registers the real `AddClient` services with a local `Api:BaseUrl` and runs JS interop in loose mode, because Radzen calls JavaScript on render.
- **Http:** fake the Api with a hand-written `HttpMessageHandler` returning canned responses. Never call a real server.
- Pass `TestContext.Current.CancellationToken` to anything that takes a `CancellationToken`.

## Layout and naming

- Folders mirror the source: `src/Govern.Client/Layout/MainLayout.razor` → `tests/Govern.ClientTests/Layout/MainLayoutTests.cs`; `Features/Home/Pages/HomePage.razor` → `Features/Home/Pages/HomePageTests.cs`.
- Name the class `<TypeUnderTest>Tests` and each test `Method_Scenario_ExpectedResult`, using `Render` / `Click` / `Submit` as the method for component tests, e.g. `SidebarToggle_Clicked_CollapsesSidebar`.
- Arrange / Act / Assert blocks, one behaviour per test, `[Theory]` for input variations.

## What to test

- What the user sees and can do: rendered text, links, form validation messages, and state after clicks.
- Services and handlers with real logic (token refresh, auth state), through their public API.
- Not Radzen internals or CSS class trivia, beyond what a behaviour needs.

## Run

- `dotnet test --project tests/Govern.ClientTests` while iterating.
- Finish with the full `dotnet test Govern.slnx`.
