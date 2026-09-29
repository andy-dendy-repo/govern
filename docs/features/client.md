---
title: Blazor client
tags: [client, blazor, webassembly, radzen, csp, static-web-apps, security-headers, bunit, configuration, auth, jwt, refresh-token, web-locks, login]
code: [src/Govern.Client, src/Govern.Client/Auth, tests/Govern.ClientTests, tests/Govern.ArchitectureTests/ClientSecurityTests.cs, tests/Govern.ArchitectureTests/ClientContractTests.cs, .github/scripts/configure-client.sh, .github/workflows/release.yml]
updated: 2026-09-28
---

# Blazor client

`Govern.Client` is a standalone Blazor WebAssembly app built with Radzen components and hosted on Azure Static Web Apps. It calls the Govern Api over HTTPS with a JWT bearer header and references no server project. This document covers the client's architecture, configuration, authentication (sign-in, token refresh, user pages), Content Security Policy and tests. The game pages are planned in [game delivery](../plans/game-delivery.md) (Plan 4).

## Client architecture (CFT folder template)

The client uses feature folders, mirroring the server's 3DFT. `tests/Govern.ArchitectureTests` enforces the template (`Conventions/Templates.cs`) and the rule file is `.claude/rules/blazor-client.md`.

| Folder | Holds | Naming |
|---|---|---|
| root | `Program.cs`, `DependencyInjection.cs` (`AddClient`), `App.razor` (router), `_Imports.razor`, `GlobalUsings.cs` | only these files |
| `Layout/` | `MainLayout` (Radzen layout, header, sidebar), `NavMenu` | Razor components |
| `Components/` | shared components with no `@page` | Razor components |
| `Auth/` | `TokenStore`, `AuthSession`, `JwtAuthenticationStateProvider`, `AuthorizationMessageHandler`, the cross-tab lock, `ReturnUrl` | free-form |
| `Features/[Feature]/Pages/` | routable components, e.g. `Features/Home/Pages/HomePage.razor`, `NotFoundPage.razor` | `XxxPage` |
| `Features/[Feature]/Components/` | components used by one feature | Razor components |
| `Features/[Feature]/Clients/` | typed Api clients, the only code besides `Auth/` and their registration in `DependencyInjection.cs` that uses `HttpClient` | sealed `XxxClient` |
| `Features/[Feature]/Models/` and other utility folders | DTOs (duplicated from the Application contracts on purpose, guarded by `ClientContractTests`), options, services; root `Models/` holds `ApiResult`, `ApiError` and `ApiJson` | usual suffixes |

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

## Client authentication (sign-in and token refresh)

The client signs in against the Api's `/api/users` endpoints ([users](users.md)) and keeps the tokens in `localStorage` ([ADR 0002](../decisions/0002-auth-tokens.md)).

| Type (`src/Govern.Client/Auth`) | Role |
|---|---|
| `TokenStore` | The only code that touches `localStorage`. One JSON entry, `govern.auth` (`StoredTokens`: access token, refresh token, refresh token expiry, `RenewAt`), read again on every call so all tabs share it. A malformed entry is removed |
| `AuthSession` (singleton) | Owns every change to the stored session: sign-in (`StartAsync`), sign-out (`SignOutAsync`, `EndAsync`), handing out access tokens and refreshing them, all under the in-tab gate and the cross-tab lock. Refresh goes through the named client `Govern.Auth`, which has no auth handler, so it never recurses |
| `JwtAuthenticationStateProvider` | The signed-in user from the access token's claims (name = `email`), without validating the signature. The user counts as signed in while an unexpired refresh token is stored, even after the access token expired |
| `AuthorizationMessageHandler` | Added to every typed client: sets `Authorization: Bearer` (only for the Api origin, `Api:BaseUrl`), refreshes ahead of expiry, and on a 401 refreshes once and retries |
| `WebLocksCrossTabLock` + `wwwroot/js/auth-lock.js` | Holds the Web Lock `govern.auth.refresh` (`navigator.locks`) during session changes. A browser without Web Locks runs unlocked; a lock that fails to load or is refused throws `CrossTabLockUnavailableException` |
| `ReturnUrl` | `Sanitize` keeps only local paths for the post-login redirect (no scheme, `//`, backslash or control characters) |

### When the access token is refreshed

- **Ahead of expiry:** `RenewAt` = local time the tokens arrived + the token's own lifetime (`exp` − `iat`) − 1 minute (`AuthSession.RenewMargin`). Measuring the lifetime on the token instead of comparing the Api's `accessTokenExpiresAt` with the local clock makes a skewed browser clock harmless. A request after `RenewAt` refreshes first, which also gives an SSE reconnect a fresh token.
- **After a 401:** the handler refreshes once and resends the request (the content is buffered before the first send); a second 401 is returned as is.
- **Single flight:** refreshes run one at a time, in the tab (a semaphore) and across tabs (the Web Lock). Inside the lock the stored tokens are read again: if another caller or tab already replaced the rejected token with a fresh one, that token is used without calling the Api. Refresh tokens are single-use, so two tabs presenting the same one would look like token reuse, and the Api would revoke every session (EventId 1001).
- **Not cancellable once sent:** the Api may already have rotated the tokens, so a refresh always finishes and stores its answer; a caller's cancellation (a closed page, a timeout) only stops it from waiting.
- **Compare-and-set:** the new pair is stored only if the stored refresh token is still the one presented, and a 400/401 clears the session only in that case, so a refresh never overwrites or clears a session another tab has just started.
- **Lock unavailable:** when the lock module fails to load or the lock is refused, the refresh is not attempted (outcome: unavailable) rather than risk two tabs presenting the same token.
- **Outcomes:**

| Refresh response | Result for the caller | Session |
|---|---|---|
| 200 | the request is sent with the new token | new tokens stored |
| 400 or 401 | 401 | cleared; `AuthorizeRouteView` sends the user to login |
| 429, 5xx, network error, timeout or lock unavailable | 503 (an unexpired current token is still used) | kept; this tab calls refresh again only after `AuthSession.UnavailableBackoff` (30 s) |

- **No re-render on refresh:** a refresh keeps the same user, so it does not raise `AuthenticationStateChanged`. Sign-in and sign-out do, and so does an Api call that finds another tab changed the user (signed out, or signed in as someone else: `JwtAuthenticationStateProvider.NotifyIfUserChanged` compares `sub`), always with an already completed state: `AuthorizeRouteView` shows its `Authorizing` content while the state task is pending, which would tear down and re-create the current page (and, later, its game stream).
- **Requests sent as is:** sign-up and sign-in set `AuthorizationMessageHandler.SendAsIs`, so they carry no bearer token and their 401 (`user.invalid_credentials`) never triggers a refresh. Sign-out sets it too, because it carries the tokens `AuthSession` chose. Without a stored session, other requests get a 401 without reaching the Api.
- **Rate limit:** sign-up, sign-in and refresh share the Api's `auth` limit (10 per minute per IP), which is why a 429 from refresh keeps the session.

### Typed clients and Api errors

- Typed clients (`Features/[Feature]/Clients`, e.g. `UsersClient`) return `ApiResult` or `ApiResult<T>` and never throw `HttpRequestException`.
- `ApiError` carries the status, the Api's `code` (e.g. `user.email_taken`), a message (the ProblemDetails `title`, or a generic text for an empty body such as the rate limiter's 429) and field errors from a validation problem. Status 0 means the Api was unreachable.
- `ApiJson.Options` matches the Api's JSON: web defaults and camelCase string enums.
- `Components/ApiErrorAlert` shows an `ApiError` as plain text.
- `AddClient` registers typed clients with `AddHttpClient<T>` plus the handler, then removes the plain `HttpClient` registration that `AddHttpClient` adds.

### User pages and the login redirect

| Route | Page | Access | Behaviour |
|---|---|---|---|
| `/login` | `LoginPage` | anonymous | Signs in, then goes to the sanitized `returnUrl` plus the login URL's fragment; `registered=true` shows "account created" |
| `/register` | `RegisterPage` | anonymous | Email, password (8–128), confirmation. Signs up, then signs in; if that sign-in fails (e.g. 429) it goes to `login?registered=true` |
| `/profile` | `ProfilePage` | `[Authorize]` | Shows `GET /api/users/me`; sign out, and sign out everywhere |

- `App.razor` uses `AuthorizeRouteView`. An anonymous visitor of an `[Authorize]` page gets `Components/RedirectToLogin`, which replaces the URL with `login?returnUrl=<path and query>` and keeps the original fragment as the login URL's fragment. A fragment (e.g. a future invite code) is never sent to a server, so it never moves into the query.
- **Sign out** (`SignOutButton`, header and profile) runs `AuthSession.SignOutAsync` under the lock: if the access token is due it refreshes first, then sends `POST /api/users/sign-out` with the **current** access and refresh tokens, and always clears the local session, even when the Api call fails. Sending a refresh token that a refresh had already rotated would count as reuse and sign the user out on every device.
- **Sign out everywhere** (profile) clears the local session only when `POST /api/users/sign-out-everywhere` succeeded (or the Api no longer accepts this session); otherwise it shows the error and stays signed in, so the user can retry, since this is the control used after losing a device.
- The header shows the email and Sign out, or a Sign in link; the nav shows Profile, or Sign in and Register.
- Not built yet: password reset and email confirmation (see [session plans](../plans/session-and-email-verification.md)). A sign-out or sign-in in another tab updates this tab's UI on its next Api call, not immediately (no `storage` event listener yet).

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
| `tests/Govern.ClientTests` (bUnit) | layout (body, navigation, sidebar toggle, signed-in and anonymous header and nav), `HomePage`/`NotFoundPage`, `App` with the real auth state (redirect to login, signed-in page), `AddClient` (URL validation, no bare `HttpClient`, resolvable services), `Auth/` (token store, JWT payload, auth state, refresh single flight, outcomes, backoff, compare-and-set and completion after the caller gave up, `RenewAt`, cross-tab user changes, the handler's bearer, origin check, retry and 503, the Web Lock module, `ReturnUrl`), sign-out with a due token sending the rotated tokens, `ApiError`, `UsersClient`, the login, register and profile pages, `RedirectToLogin`, `SignOutButton`, `ApiErrorAlert`. `Support/` fakes the Api (`FakeApiHandler`), browser storage (`FakeBrowserStorage`) and time (`FakeTimeProvider`) |
| `tests/Govern.ArchitectureTests` | CFT folders, page/component/client naming, `System.Net.Http` and `Microsoft.JSInterop` confinement, `.razor` size limits, and `ClientSecurityTests` (CSP directives, no unsafe script sources, headers, no route header overrides, `/.auth/*` blocked, `index.html` without external assets or inline scripts, no raw HTML or eval in `.cs`, `.razor` or `wwwroot` JavaScript, no HTML or code sinks in `wwwroot` JavaScript, `localStorage` only in `Auth/TokenStore.cs`, no secrets in appsettings), and `ClientContractTests` (a client DTO named like an Application contract has only that contract's properties, with the same shapes; `TokenPair` and the refresh body are paired explicitly with the sign-in and refresh contracts) |

## Running the client locally

```bash
dotnet run --project src/Govern.Api      # https://localhost:7294
dotnet run --project src/Govern.Client   # https://localhost:7042 (http://localhost:5114)
```

The dev server doesn't apply `staticwebapp.config.json`, so the CSP is only active on the Static Web App. After a release, `curl -I https://<static-web-app-host>` shows the headers, and DevTools must show no CSP violations.
