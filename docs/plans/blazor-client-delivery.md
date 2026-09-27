---
title: Blazor client delivery plans
tags: [plan, backlog, follow-ups, blazor, radzen, csp, static-web-apps, auth, jwt, localstorage]
code: [src/Govern.Client, tests/Govern.ArchitectureTests, .github/workflows/release.yml]
updated: 2026-09-27
---

# Blazor client delivery plans

Follow-up #10 (the Blazor WebAssembly client, roadmap step 9) split into two increments. They run last in the [follow-ups delivery order](follow-ups-delivery.md). Each increment gets its own branch from `stage` and goes through `/develop-feature`. When a plan merges, it removes its section here; the second one deletes this file and its index line.

## Plan 6: Blazor client foundation and CSP gate (#10a)

### Context
`Govern.Client` is still the stock template. Before any token is stored in localStorage (ADR 0002), the Static Web App must serve a strict CSP and security headers. This plan sets up Radzen, configuration, the SWA config and the client rules, with no auth yet.

### Scope
- In:
  - remove the template pages and Bootstrap
  - Radzen (package, theme, `<RadzenComponents/>`), layout shell
  - `Api:BaseUrl` configuration, `staticwebapp.config.json` (CSP from `follow-ups.md` §10, security headers, navigation fallback)
  - the client rule and folder spec
- Out: auth and pages (Plan 7).

### Affected areas
- **Client:**
  - `src/Govern.Client/{Program.cs, App.razor, _Imports.razor, Layout/*, Pages/*, wwwroot/index.html, wwwroot/appsettings.json}`
  - new `wwwroot/staticwebapp.config.json` with the placeholder `__API_ORIGIN__` in `connect-src`
- **Workflow:** in `release.yml`, before the SWA deploy, substitute the Api origin (Bicep output `apiHostname`, or a repository variable when a custom domain exists) into `staticwebapp.config.json`, and write `wwwroot/appsettings.Production.json` with `Api:BaseUrl`.
- **Tests:**
  - `tests/Govern.ArchitectureTests/Conventions/Templates.cs` gets the Client folder spec: `Pages`, `Layout`, `Components`, `Services`, `Models`, `wwwroot`.

### Steps
1. **Client:** clean-up, Radzen and config.
2. **Client:** the SWA config.
3. **Workflow:** the substitution step. Fail the job if the placeholder is still present.
4. **Arch tests and rules.**

### Tests
- **Architecture:**
  - the Client folder spec
  - new `ClientSecurityTests`: `staticwebapp.config.json` CSP contains `default-src 'self'`, `script-src 'self' 'wasm-unsafe-eval'`, `frame-ancestors 'none'` and `connect-src 'self' __API_ORIGIN__`
  - `X-Content-Type-Options`, `Referrer-Policy`, and no `unsafe-inline` for scripts
  - no `MarkupString` in the Client sources
- **Unit and integration:** none (the client has no server logic).

### Docs & rules
- New `.claude/rules/blazor-client.md` (folders, Radzen only, no raw HTML, config, tokens only through the token store) plus its entry in CLAUDE.md "Claude setup".
- New `docs/features/client.md`.
- `docs/operations/deployment.md` (the client config step).
- Roadmap step 9 status.

### Verification
- Build and all tests.
- `dotnet run --project src/Govern.Client`: the shell renders with the Radzen theme.
- After release: `curl -I` on the SWA shows the CSP headers.

### Risks & open questions
- Radzen may need `'unsafe-inline'` for **styles**. Allow it for `style-src` only if needed, never for `script-src`.

---

## Plan 7: Blazor client auth and pages (#10b)

### Context
With the CSP gate in place, the client can sign users in. Tokens live in localStorage (ADR 0002). A delegating handler attaches the bearer token and refreshes once on 401. The pages are login, register, profile and sign-out.

### Scope
- In:
  - `TokenStore` (localStorage via `IJSRuntime`)
  - `JwtAuthenticationStateProvider` (reads claims, no signature validation)
  - `AuthorizationMessageHandler`: Bearer header, one single-flight refresh on 401 (`SemaphoreSlim`), and on refresh failure it clears tokens and redirects to login
  - a typed `UsersClient`, with DTOs duplicated in `Client/Models`
- Pages:
  - Login, Register, Profile (`GetCurrentUser`)
  - sign-out and sign-out everywhere
  - `AuthorizeView` in the layout
  - Confirm-email page **if** session Plan 3 has merged
- Out: password reset, and any UI for server features not yet built.

### Affected areas
- **Client:**
  - `Services/{TokenStore, JwtAuthenticationStateProvider, AuthorizationMessageHandler, UsersClient}.cs`
  - `Models/*`, `Pages/{Login, Register, Profile}.razor`, `Layout/MainLayout.razor`, `Program.cs`
  - `Microsoft.AspNetCore.Components.Authorization` via `dotnet add package`

### Steps
1. **Client:** services and DI.
2. **Client:** pages and layout.
3. **Api:** none. CORS already allows the SWA origin (`infra/main.bicep:95`).

### Tests
- **Architecture:**
  - Client services follow the folder spec.
  - Only `TokenStore` touches `localStorage` (a source scan for `localStorage` outside `TokenStore`).
- **Integration:** none new. The Api contracts the client uses are covered by existing endpoint tests.
- **Manual:** the full flow against the local Api (register → login → profile → wait for the access token to expire or tamper with it → auto refresh → sign-out).

### Docs & rules
- `docs/features/client.md` (auth flow).
- `docs/decisions/0002-auth-tokens.md` (the implementation link).
- `blazor-client.md` (token store rule).
- Remove #10 from follow-ups and delete the follow-ups docs if they are empty.

### Verification
- Build and all tests.
- `dotnet run` both projects and walk the manual flow.
- After release: sign in on the SWA URL and check that DevTools shows no CSP violations.

### Risks & open questions
- The DTOs are duplicated between Client and Application, and drift is caught only manually or by the flow check. A shared contracts project is deferred.
