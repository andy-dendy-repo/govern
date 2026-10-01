---
paths:
  - "tests/Govern.ClientTests/**"
---

# Client tests (bUnit)

**Scope:** `tests/Govern.ClientTests` tests `Govern.Client` only: Razor components, client services, typed Api clients and auth plumbing. It may reference only `Govern.Client` (`ProjectReferenceTests.ClientTests_ReferenceOnlyClient`). The Api contracts the client calls are covered by the Api integration tests, never here.

## Stack

- xUnit v3 on Microsoft Testing Platform, **bUnit** (`BunitContext`, `Render<T>`), Shouldly. `Xunit`, `Shouldly` and `Bunit` are global usings.
- **Assertions:** Shouldly on the rendered DOM (`cut.Find("h1").TextContent.ShouldBe(…)`). Don't use `Assert.*` or brittle full-markup snapshots of Radzen output.
- **Context:** component, client and auth tests derive from `Support/ClientTestContext`. It registers the real `AddClient` services with a local `Api:BaseUrl`, runs JS interop in loose mode (Radzen calls JavaScript on render), and replaces:
  - the Api: every typed and named `HttpClient` talks to `Api` (`Support/FakeApiHandler`). Script responses with `Api.On(method, path, …)` using `FakeApiHandler.Json/Problem/ValidationProblem/Empty`, and assert on `Api.Requests` / `Api.RequestsTo(path)` (method, path, `Authorization`, body). An unscripted request throws. Never call a real server.
  - browser storage: `Storage` (`Support/FakeBrowserStorage`, a bUnit invocation handler) answers the `localStorage` calls from a dictionary, so the real `TokenStore` works. `SignIn(email)` stores a fresh session and marks the user signed in; `StoreTokens` / `StoredTokens()` write and read the entry.
  - time: `Time` is a `FakeTimeProvider` registered as `TimeProvider` (e.g. advance 14 minutes to make the access token due).
  - event streams: `Support/SseResponse` is an open `text/event-stream` body the test writes to (`WriteGameAsync`, `WriteHeartbeatAsync`, `WriteRawAsync`) and ends (`Complete`, or `Fail` for a reset). Return `stream.Response` from `Api.On`; one instance per connection, so script one per expected reconnect.
  - the cross-tab lock: `CrossTabLock` (`Support/FakeCrossTabLock`) runs the action directly and counts calls; set `Unavailable` to simulate a refused lock.
  - authentication state: `Authorization` is bUnit's `AddAuthorization()` context (anonymous until `SignIn`), which feeds `AuthorizeView`. Pass `fakeAuthorization: false` to the base constructor to keep the real `JwtAuthenticationStateProvider` (driven by `Storage`) and real authorization services, e.g. `AppTests` rendering `App` through `AuthorizeRouteView`.
- Build JWTs with `Support/TestTokens` (unsigned, Api-shaped claims) instead of hand-written strings.
- Game views: `Support/TestViews` builds `PlayerView`s by hand on an 8×6 all-plains map (`Start()`, `Create(units, cities, visibility, terrain, rivers, resources, improvements, bridges)`, `Finished(winner)`, and `with` for the turn or status); `TestGames.Started(phase, view)` wraps one with player ids that match. Compare views by `GameSerializer.Serialize(view)`, since `ImmutableArray` compares by reference.
- JavaScript modules: `JSInterop.SetupModule(BoardInteropService.ModulePath)` (or `WebLocksCrossTabLock.ModulePath`) and set up each export (`SetupVoid("init", _ => true)`, `Setup<BoardRect?>("measure", _ => true)`); count imports with `JSInterop.Invocations`. Board gestures use bUnit's `PointerDown`/`PointerMove`/`PointerUp`/`Wheel` on the `svg`, and tiles are found by `use.tile[data-q='…'][data-r='…']`.
- Game UI: `Features/Games/Services/GameUiService` is a plain class (test it with `new`); `Components/Game/GameSpriteTests` checks every ruleset unit, terrain, improvement, building and resource and every `Helpers/Icons` constant has a symbol; pages that inject `IWebAssemblyHostEnvironment` (`DesignPage`) get a fake registered by the test.
- Background loops with timers (`GameWatchService`): only advance `Time` once the loop is waiting on the timer you mean, i.e. after the request or update that precedes it has been recorded. Wait with `Support/Eventually.HoldsAsync(condition, because)`; check that something did **not** happen with `Eventually.NeverAsync`. Record an update only after asking the enumerator for the next one (see `GameWatchServiceTests.Watch`), so the delay that follows it already exists.
- Navigation history: cast the `NavigationManager` to `BunitNavigationManager` and assert on `History` (a `replace` navigation replaces the top entry) to prove a URL with a secret fragment is gone.
- Async component flows (submit, click, then an Api call) finish off the render thread: assert with `cut.WaitForAssertion(…)`. Navigate with the bUnit `NavigationManager` before rendering to set the URL, query and fragment.
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
