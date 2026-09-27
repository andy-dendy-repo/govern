---
paths:
  - "src/Govern.Client/**"
---

# Blazor client (Govern.Client)

`Govern.Client` is a standalone Blazor WebAssembly app using Radzen components, hosted on Azure Static Web Apps. It talks to the Api over HTTPS with a JWT bearer header and references **no** server project. It stores tokens in localStorage (ADR 0002), so **XSS prevention is a security requirement**. The folder, naming, dependency, size and security rules below are enforced by `tests/Govern.ArchitectureTests` (change the rule and the test together); "Radzen only" and "no `ConfigureAwait(false)`" are checked in review.

## Folders (CFT, client folder template)

```
Govern.Client/
├── Program.cs, DependencyInjection.cs, GlobalUsings.cs, App.razor, _Imports.razor   (only root files)
├── Layout/                  layout components: MainLayout, NavMenu (flat)
├── Components/              shared components with no @page (one level of group folders allowed)
├── Auth/                    token store, auth state provider, bearer/refresh message handler (free-form)
├── Features/[Feature]/      UF +
│   ├── Pages/               routable components: XxxPage.razor (+ .razor.cs / .razor.css), flat
│   ├── Components/          components used only by this feature
│   └── Clients/             typed Api clients: XxxClient.cs, flat
├── Helpers Models Enums Options Exceptions Extensions Attributes Services   (UF, free-form, usual suffixes)
└── wwwroot/                 index.html, css/app.css, appsettings*.json, staticwebapp.config.json
```

| Where | Must |
|---|---|
| `Features/[Feature]/Pages` | components named `XxxPage` (e.g. `LoginPage`, `NotFoundPage`), file `XxxPage.razor` |
| `Layout`, `Components`, `Features/[Feature]/Components` | Razor components only (no plain classes; logic goes to `Services` or a client) |
| `Features/[Feature]/Clients` | `sealed` classes ending with `Client` |
| UF folders | the usual suffixes from `architecture.md` (`Options`, `Service`, …) |

- Only components, their code-behind (`.razor.cs`) and scoped styles (`.razor.css`) sit next to each other. Scoped styles need `<link rel="stylesheet" href="Govern.Client.styles.css" />` in `index.html`; it was removed while no `.razor.css` exists, so add it back with the first one.
- DTOs for Api calls live in `Features/[Feature]/Models`. They duplicate the Application contracts on purpose, because the client references no server project.

## Components

- **Radzen only** (`Radzen.Blazor`): `RadzenStack`, `RadzenText`, `RadzenButton`, `RadzenTemplateForm`, … No Bootstrap and no other UI kits. Use plain HTML elements only for structure Radzen doesn't cover.
- **Never render raw HTML:** no `MarkupString`, `RenderTreeBuilder.AddMarkupContent`, `RadzenHtmlEditor` or `RadzenMarkdown`, and no `eval`/`Function` through `IJSRuntime`. Render user data as text so Blazor encodes it.
- Keep `@code` blocks at most 40 lines (`CodeLimits.MaxRazorCodeBlockLines`) and `.razor` files at most 300 lines. Move bigger logic to a `.razor.cs` partial code-behind or a service.
- Public members in code-behind need XML docs (CS1591); generated Razor code doesn't.
- Never `ConfigureAwait(false)`: components must resume on the renderer's context (CA2007 is off for the client).

## Http, JS interop and tokens

- **`HttpClient`** (anything in `System.Net.Http`) is used only by `Features/[Feature]/Clients/*Client`, `Auth/`, and the root `DependencyInjection` to register them. Components inject a typed client, never `HttpClient`; typed clients return results, not `HttpRequestException`. `AddClient` registers no bare `HttpClient`.
- **`IJSRuntime`** is used only in `Auth/` and `Services/` (root or `Features/[Feature]/Services`).
- **Tokens** are read and written only through `Auth/TokenStore.cs` (Plan 7). No other `.cs`, `.razor`, `wwwroot/**/*.js` or `index.html` may mention `localStorage`, even in a string passed to `IJSRuntime` (`ClientSecurityTests.ClientSources_TouchLocalStorageOnlyInTokenStore`). Everything else injects `TokenStore`.

## Configuration

- `Api:BaseUrl` binds to `Options/ApiOptions`. It must be absolute HTTPS, or HTTP for a loopback host only; `AddClient` throws on start otherwise.
- `wwwroot/appsettings.json` holds the local Api URL (`https://localhost:7294`). `wwwroot/appsettings.Production.json` holds the `__API_ORIGIN__` placeholder, which the release workflow replaces. It must exist at build time, because Blazor only fetches appsettings files listed in its boot manifest.
- Everything in `wwwroot/appsettings*.json` is public: URLs and client ids only, never keys, secrets or tokens (`ClientSecurityTests` rejects such keys).

## CSP and hosting (`wwwroot/staticwebapp.config.json`)

- CSP:
  - `default-src 'self'`
  - `script-src 'self' 'wasm-unsafe-eval' __IMPORTMAP_HASH__`
  - `style-src 'self'; style-src-attr 'unsafe-inline'`: Radzen sets inline `style` attributes, verified to break without it. Inline `<style>` elements stay blocked; if a Radzen component ever needs one (a CSP error in the console), hash it in `style-src` rather than allowing `'unsafe-inline'` there.
  - `img-src 'self' data:`, `font-src 'self'`
  - `connect-src 'self' __API_ORIGIN__`
  - `frame-ancestors 'none'`, `base-uri 'self'`, `form-action 'self'`, `object-src 'none'`
- Also sends `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, `Permissions-Policy`, HSTS and COOP, plus the navigation fallback to `/index.html`.
- `routes` only blocks the Static Web App's built-in auth (`/.auth/*` → 404). Routes never set `headers`, because a route-level CSP replaces the global one.
- `.github/scripts/configure-client.sh` fills the placeholders after `dotnet publish`: the Api origin, and the SHA-256 of the import map that publish writes inline into `index.html`. CI dry-runs it on every PR.
- `index.html` has **no inline scripts or `on*=` handlers** besides the empty `<script type="importmap">`, and **no external assets**. Everything is self-hosted from `_content/…` or `wwwroot`, including Radzen's fonts. No script directive (`script-src`, `script-src-elem`, `script-src-attr`, `worker-src`, `default-src`) ever gets `'unsafe-inline'`, `'unsafe-eval'`, wildcards or scheme sources.

## Tests

bUnit component tests live in `tests/Govern.ClientTests` (see `client-tests.md`).
