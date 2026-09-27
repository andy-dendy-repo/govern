---
title: Blazor client
tags: [client, blazor, webassembly, radzen, csp, static-web-apps, security-headers, bunit, configuration]
code: [src/Govern.Client, tests/Govern.ClientTests, tests/Govern.ArchitectureTests/ClientSecurityTests.cs, .github/scripts/configure-client.sh, .github/workflows/release.yml]
updated: 2026-09-27
---

# Blazor client

`Govern.Client` is a standalone Blazor WebAssembly app built with Radzen components and hosted on Azure Static Web Apps. It calls the Govern Api over HTTPS with a JWT bearer header and references no server project. This document covers the client foundation: its architecture, configuration, Content Security Policy and tests. Authentication and the user pages are delivered next ([Blazor client delivery plans](../plans/blazor-client-delivery.md), Plan 7).

## Client architecture (CFT folder template)

The client uses feature folders, mirroring the server's 3DFT. `tests/Govern.ArchitectureTests` enforces the template (`Conventions/Templates.cs`) and the rule file is `.claude/rules/blazor-client.md`.

| Folder | Holds | Naming |
|---|---|---|
| root | `Program.cs`, `DependencyInjection.cs` (`AddClient`), `App.razor` (router), `_Imports.razor`, `GlobalUsings.cs` | only these files |
| `Layout/` | `MainLayout` (Radzen layout, header, sidebar), `NavMenu` | Razor components |
| `Components/` | shared components with no `@page` | Razor components |
| `Auth/` | token store, auth state provider, bearer and refresh message handler (Plan 7) | free-form |
| `Features/[Feature]/Pages/` | routable components, e.g. `Features/Home/Pages/HomePage.razor`, `NotFoundPage.razor` | `XxxPage` |
| `Features/[Feature]/Components/` | components used by one feature | Razor components |
| `Features/[Feature]/Clients/` | typed Api clients, the only code besides `Auth/` and their registration in `DependencyInjection.cs` that uses `HttpClient` | sealed `XxxClient` |
| `Features/[Feature]/Models/` and other utility folders | DTOs (duplicated from the Application contracts on purpose), options, services | usual suffixes |

Further rules for Blazor components:
- `IJSRuntime` is used only in `Auth/` and `Services/` (root or feature).
- `localStorage`, where the tokens are kept, is touched only by `Auth/TokenStore.cs`. Other code injects `TokenStore`; a source scan of `.cs`, `.razor`, `wwwroot` JavaScript and `index.html` enforces it.
- Components keep `@code` blocks at most 40 lines; larger logic moves to a `.razor.cs` code-behind or a service.

## Client configuration (Api:BaseUrl)

| Key | File | Value | Purpose |
|---|---|---|---|
| `Api:BaseUrl` | `wwwroot/appsettings.json` | `https://localhost:7294` | Local Api (Development) |
| `Api:BaseUrl` | `wwwroot/appsettings.Production.json` | `__API_ORIGIN__`, replaced at release | Production Api origin |

- **Binding and validation:** the value binds to `Options/ApiOptions`. `AddClient` fails at startup unless the URL is absolute and HTTPS, without credentials, query or fragment; HTTP is allowed for loopback hosts only.
- **The production file must exist at build time:** Blazor fetches only the appsettings files listed in its boot manifest, so the file can't be created after publish.
- **Everything here is public:** every visitor downloads these files, so they hold URLs only. `ClientSecurityTests` rejects keys that look like secrets.
- **Api CORS:** the Api allows the local client origins (`https://localhost:7042`, `http://localhost:5114`) through `Cors:AllowedOrigins` in its Development settings.

## Content Security Policy and security headers

The client stores tokens in `localStorage` ([ADR 0002](../decisions/0002-auth-tokens.md)), so an XSS would leak them. `wwwroot/staticwebapp.config.json` makes the Static Web App send these `globalHeaders`:

| Header | Value |
|---|---|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' 'wasm-unsafe-eval' <import map hash>; style-src 'self'; style-src-attr 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' <api origin>; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'; upgrade-insecure-requests` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `X-Frame-Options` | `DENY` |
| `Permissions-Policy` | camera, microphone, geolocation, payment and usb disabled |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `Cross-Origin-Opener-Policy` | `same-origin` |

- **`'wasm-unsafe-eval'`:** the .NET WebAssembly runtime needs it to compile WebAssembly. Scripts never get `'unsafe-inline'` or `'unsafe-eval'`.
- **Import map hash:** `dotnet publish` writes an inline `<script type="importmap">` into `index.html`, mapping fingerprinted framework files. Its SHA-256 is added to `script-src` at release; it is the only inline script allowed.
- **`style-src-attr 'unsafe-inline'`:** Radzen components set inline `style` attributes, and the layout breaks without them (verified in headless Chrome). Only style *attributes* are allowed; injected `<style>` elements and all inline scripts stay blocked.
- **Self-hosted assets:** the Radzen theme (`_content/Radzen.Blazor/css/material.css`), its fonts and `Radzen.Blazor.js` come from the app's own origin, so there are no CDNs.
- **Built-in Static Web Apps auth disabled:** `routes` answers `/.auth/*` with 404, because Govern authenticates against its own Api. Routes never set headers, since a route-level CSP would replace the global one.
- **Navigation fallback:** unknown paths rewrite to `/index.html`, so deep links reach the Blazor router, which renders `NotFoundPage` for unknown routes. Framework, content and static files are excluded.

## Configuring the client at release

`.github/scripts/configure-client.sh <published wwwroot> <api origin>` runs in `release.yml` after `dotnet publish src/Govern.Client`:
1. It checks that the Api origin is lower-case `https://host` without port or path (the release workflow validates `API_ORIGIN` the same way before deploying). The origin is the repository variable `API_ORIGIN` when set (custom domain), otherwise `https://<apiHostname>` from the Bicep outputs.
2. It computes the SHA-256 of the import map in `index.html`, failing if `index.html` has any other inline script.
3. It replaces `__API_ORIGIN__` and `__IMPORTMAP_HASH__` in `staticwebapp.config.json` and `appsettings.Production.json`, and deletes their stale `.br`/`.gz` copies.
4. It fails the job if a placeholder is left.

CI runs the same script on every PR against a dummy origin, so a change that adds an inline script fails before release.

## Client tests

| Project | What it covers |
|---|---|
| `tests/Govern.ClientTests` (bUnit) | layout renders the body and navigation, sidebar toggle, `HomePage`/`NotFoundPage` headings, `AddClient` URL validation (HTTPS or loopback, no credentials/query/fragment) and no bare `HttpClient` |
| `tests/Govern.ArchitectureTests` | CFT folders, page/component/client naming, `System.Net.Http` and `Microsoft.JSInterop` confinement, `.razor` size limits, and `ClientSecurityTests` (CSP directives, no unsafe script sources, headers, no route header overrides, `/.auth/*` blocked, `index.html` without external assets or inline scripts, no raw HTML or eval, `localStorage` only in `Auth/TokenStore.cs`, no secrets in appsettings) |

## Running the client locally

```bash
dotnet run --project src/Govern.Api      # https://localhost:7294
dotnet run --project src/Govern.Client   # https://localhost:7042 (http://localhost:5114)
```

The dev server doesn't apply `staticwebapp.config.json`, so the CSP is only active on the Static Web App. After a release, `curl -I https://<static-web-app-host>` shows the headers, and DevTools must show no CSP violations.
