---
title: Blazor client delivery plans
tags: [plan, backlog, follow-ups, blazor, radzen, csp, static-web-apps, auth, jwt, localstorage]
code: [src/Govern.Client, tests/Govern.ClientTests, tests/Govern.ArchitectureTests]
updated: 2026-09-27
---

# Blazor client delivery plans

Follow-up #10 (the Blazor WebAssembly client, roadmap step 9) was split into two increments. They run last in the [follow-ups delivery order](follow-ups-delivery.md). Plan 6 (foundation, CFT architecture, CSP gate, bUnit) is done: see [client](../features/client.md). The remaining increment gets its own branch from `stage` and goes through `/develop-feature`; when it merges, it deletes this file and its index line.

## Plan 7: Blazor client auth and pages (#10b)

### Context
With the CSP gate in place, the client can sign users in. Tokens live in localStorage (ADR 0002). A delegating handler attaches the bearer token and refreshes once on 401. The pages are login, register, profile and sign-out.

### Scope
- In:
  - `TokenStore` (localStorage via `IJSRuntime`)
  - `JwtAuthenticationStateProvider` (reads claims, no signature validation)
  - `AuthorizationMessageHandler`: Bearer header, one single-flight refresh on 401 (`SemaphoreSlim`), and on refresh failure it clears tokens and redirects to login
  - a typed `UsersClient` (`Features/Users/Clients`), with DTOs duplicated in `Features/Users/Models`
- Pages:
  - Login, Register, Profile (`GetCurrentUser`)
  - sign-out and sign-out everywhere
  - `AuthorizeView` in the layout
  - Confirm-email page **if** session Plan 3 has merged
- Out: password reset, and any UI for server features not yet built.

### Affected areas
Folders follow the client template (CFT, `.claude/rules/blazor-client.md`).
- **Client:**
  - `Auth/{TokenStore, JwtAuthenticationStateProvider, AuthorizationMessageHandler}.cs`
  - `Features/Users/Clients/UsersClient.cs` (sealed, the only user of `HttpClient` for Users), `Features/Users/Models/*` (DTOs)
  - `Features/Users/Pages/{LoginPage, RegisterPage, ProfilePage}.razor`, `Layout/MainLayout.razor` (`AuthorizeView`), `Layout/NavMenu.razor`
  - `DependencyInjection.cs`: `AddHttpClient<UsersClient>` with `ApiOptions.BaseUrl` and the message handler, plus authorization services
  - `Microsoft.AspNetCore.Components.Authorization` and `Microsoft.Extensions.Http` via `dotnet add package`
- **Tests:** `tests/Govern.ClientTests/Auth/*Tests.cs`, `Features/Users/Pages/*Tests.cs`

### Steps
1. **Client:** services and DI.
2. **Client:** pages and layout.
3. **Api:** none. CORS already allows the SWA origin (`infra/main.bicep:95`).

### Tests
- **Client (bUnit, `tests/Govern.ClientTests`):**
  - `AuthorizationMessageHandler`: adds the bearer header; on 401 refreshes once and retries; concurrent 401s share one refresh (single flight); a failed refresh clears tokens and navigates to login. Use a fake `HttpMessageHandler`.
  - `JwtAuthenticationStateProvider`: claims from a stored token, anonymous without one or with an expired one.
  - `TokenStore`: reads and writes through bUnit's JS interop (planned invocations).
  - Pages: login and register validation messages, error display for Api problem details, profile renders the current user.
- **Architecture:**
  - Existing rules already cover `Features/*/Clients`, `Auth/` and the `HttpClient`/`IJSRuntime` confinement.
  - Already in place: only `Auth/TokenStore.cs` may mention `localStorage` (`ClientSecurityTests.ClientSources_TouchLocalStorageOnlyInTokenStore`). Name the file exactly `TokenStore.cs`, or update the test and `blazor-client.md` together.
- **Integration:** none new. The Api contracts the client uses are covered by existing endpoint tests.
- **Manual:** the full flow against the local Api (register → login → profile → wait for the access token to expire or tamper with it → auto refresh → sign-out).

### Docs & rules
- `docs/features/client.md` (auth flow).
- `docs/decisions/0002-auth-tokens.md` (the implementation link).
- `blazor-client.md` and `client-tests.md` (token store and auth test patterns).
- Remove #10 from follow-ups and delete the follow-ups docs if they are empty. Delete this file and its `docs/README.md` line.

### Verification
- Build and all tests.
- `dotnet run` both projects and walk the manual flow.
- After release: sign in on the SWA URL and check that DevTools shows no CSP violations.

### Risks & open questions
- The DTOs are duplicated between Client and Application, and drift is caught only manually or by the flow check. A shared contracts project is deferred.
