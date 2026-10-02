---
title: Blazor client
tags: [client, blazor, webassembly, radzen, csp, static-web-apps, security-headers, bunit, configuration, auth, jwt, refresh-token, web-locks, login, games, lobby, invite-link, sse, server-sent-events, reconnect, backoff, board, hex, webgl, three-js, pan, zoom, commands, stale-version]
code: [src/Govern.Client, src/Govern.Client/Auth, src/Govern.Client/Features/Games, src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/wwwroot/js/board3d, src/Govern.Client/wwwroot/lib/three, tests/Govern.ClientTests, tests/Govern.ArchitectureTests/ClientSecurityTests.cs, tests/Govern.ArchitectureTests/ClientContractTests.cs, .github/scripts/configure-client.sh, .github/workflows/release.yml]
updated: 2026-10-02
---

# Blazor client

`Govern.Client` is a standalone Blazor WebAssembly app built with Radzen components and hosted on Azure Static Web Apps. It calls the Govern Api over HTTPS with a JWT bearer header and references no server project. This document covers the client's architecture, configuration, authentication (sign-in, token refresh, user pages), the game lobby (my games, invite links, live updates), the game board (hex map, selection, commands), Content Security Policy and tests.

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
| `Features/[Feature]/Clients/` | typed Api clients, e.g. `UsersClient`, `GamesClient`. The only code besides `Auth/`, the shared send (`Extensions/HttpClientExtensions`) and the registration in `DependencyInjection.cs` that uses `HttpClient` or server-sent events (`System.Net.ServerSentEvents`) | sealed `XxxClient` |
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

- Typed clients (`Features/[Feature]/Clients`, e.g. `UsersClient`) return `ApiResult` or `ApiResult<T>` and never throw `HttpRequestException`. They send through `Extensions/HttpClientExtensions.SendForResultAsync`, which maps a ProblemDetails body to `ApiError` and network errors, unreadable JSON and timeouts to `ApiError.Network`; the caller's own cancellation still throws.
- `ApiError` carries the status, the Api's `code` (e.g. `user.email_taken`), a message (the ProblemDetails `title`, or a generic text for an empty body such as the rate limiter's 429) and field errors from a validation problem. Status 0 means the Api was unreachable.
- `ApiJson.Options` matches the Api's JSON: web defaults and camelCase string enums.
- `Components/ApiErrorAlert` shows an `ApiError` as plain text.
- `AddClient` registers typed clients with `AddHttpClient<T>` plus the handler, then removes the plain `HttpClient` registration that `AddHttpClient` adds.

### User pages and the login redirect

| Route | Page | Access | Behaviour |
|---|---|---|---|
| `/login` | `LoginPage` | anonymous | Signs in, then goes to the sanitized `returnUrl` plus the login URL's fragment, replacing the login history entry; `registered=true` shows "account created" |
| `/register` | `RegisterPage` | anonymous | Email, password (8–128), confirmation. Signs up, then signs in; if that sign-in fails (e.g. 429) it goes to `login?registered=true`. Either way it replaces the register history entry |
| `/profile` | `ProfilePage` | `[Authorize]` | Shows `GET /api/users/me`; sign out, and sign out everywhere |

- `App.razor` uses `AuthorizeRouteView`. An anonymous visitor of an `[Authorize]` page gets `Components/RedirectToLogin`, which replaces the URL with `login?returnUrl=<path and query>` and keeps the original fragment as the login URL's fragment. A fragment (e.g. an invite code) is never sent to a server, so it never moves into the query. After a successful sign-in the login and register pages navigate with `replace`, so Back doesn't return to the form or to a URL that still carries the fragment.
- **Sign out** (`SignOutButton`, header and profile) runs `AuthSession.SignOutAsync` under the lock: if the access token is due it refreshes first, then sends `POST /api/users/sign-out` with the **current** access and refresh tokens, and always clears the local session, even when the Api call fails. Sending a refresh token that a refresh had already rotated would count as reuse and sign the user out on every device.
- **Sign out everywhere** (profile) clears the local session only when `POST /api/users/sign-out-everywhere` succeeded (or the Api no longer accepts this session); otherwise it shows the error and stays signed in, so the user can retry, since this is the control used after losing a device.
- The header shows the email and Sign out, or a Sign in link; the nav shows Games, Join game and Profile, or Sign in and Register. The home page links to My games and Join a game, or to Sign in and Register.
- Not built yet: password reset and email confirmation (see [session plans](../plans/done/session-and-email-verification.md)). A sign-out or sign-in in another tab updates this tab's UI on its next Api call, not immediately (no `storage` event listener yet).

## Client game lobby (games, invite links, live updates)

The game pages live in `Features/Games` and call the [games API](games.md) through `Features/Games/Clients/GamesClient`. Every page is `[Authorize]`. The Api sends the caller's board (`view`, Core's `PlayerView`) with each started game; the page draws it (see [Client game board](#client-game-board-hex-map-selection-and-commands)).

| Route | Page | Behaviour |
|---|---|---|
| `/games` | `GamesPage` | The caller's games, newest activity first, in a server-paged `RadzenDataGrid` (20 per page, `GET /api/games?page&pageSize`): phase, players ("n/2"), role (created or joined), "Your turn", last activity in local time. "New game" creates a lobby and opens it; caps, the daily quota and 429 show in `ApiErrorAlert`. "Join with a code" opens `/join` |
| `/join` | `JoinPage` | An invite link (`/join#<code>`) fills in the form ("You were invited…"); a pasted link or code works too (`InviteLink.TryParse`: a bare code, or a link to the join page with the code in its fragment; 22 base64url characters). Joining always takes a click on Join. On success it opens the game, replacing the history entry |
| `/games/{id}` | `GamePage` | Reads the game (404 shows "does not exist"), then follows it live. In the lobby: `PlayerList`, `InviteShare` (creator only: the link and the code, each with a copy button through `Services/ClipboardService`), `LobbyPanel` (Start once both seats are taken; Cancel for the creator, Leave for the joiner, each confirmed inline first). Once started: `GameTable` (the full-screen game view: the board with its cards over it, in `Layout/GameLayout` without the sidebar; [game UI](game-ui.md)). A `ConnectionBadge` shows Live, Reconnecting in n s, Gone, Signed out or Finished |

### Invite link policy (code only in the fragment or the body)

- The creator shares `{base}join#{code}` (`Features/Games/Helpers/InviteLink.Build`). Browsers never send a fragment to a server, so the code stays out of Static Web App and Api logs, and out of `Referer`.
- `JoinPage` reads the fragment and **at once** replaces the URL with `/join` (`NavigateTo(..., replace: true)`), so the code leaves the address bar and the Back history. The code only fills in the form; a malformed fragment leaves the form empty and shows "This invite link is not valid".
- **Joining needs a click.** Opening the link never joins by itself: any site can navigate a signed-in player's window to `/join#<code>`, and an automatic join would drop them into an attacker's game (which the attacker can start at once, so it can't be left) and use up their caps and daily quota. The click sends the code in the body of `POST /api/games/join`; success opens the game (again replacing the entry), and a failure shows the error.
- The browser's own global history (and address-bar suggestions) still records the visited link: `replace` only rewrites the tab's session history. Anyone using the same browser profile could read the code there; the code stops working once the lobby is full, and the creator can cancel.
- A signed-out visitor goes through `RedirectToLogin` with the fragment kept as the login URL's fragment, and login (or register) replaces its own history entry. Clicking between login and register pushes one entry that still holds the fragment; it stays in local history only.
- Joining takes a seat from the daily quota; the joiner can leave the lobby again until the creator starts it.

### Live updates: reconnect and backoff

`GamesClient.WatchAsync` opens `GET /api/games/{id}/stream` with `Accept: text/event-stream` and browser response streaming, reads it with `SseParser`, and yields `GameSnapshotEvent` (a `game` event), `GameHeartbeatEvent` and unknown types skipped. Unless cancelled it always ends with one `GameStreamClosedEvent`:

| Stream result | `GameStreamEnd` |
|---|---|
| The Api ended the stream (finished game, the caller can no longer read it, access token expiry) | `Closed` |
| 404 | `NotFound` |
| 401 after the handler's refresh failed | `Unauthorized` |
| 429 (more than 4 open streams, or 30 openings per minute) | `RateLimited` |
| Anything else: 5xx, the handler's 503 (refresh unavailable), a 200 that isn't an event stream, network errors, bad JSON | `Failed` |

`Features/Games/Services/GameWatchService` (with `TimeProvider`) keeps it open and turns it into `GameWatchUpdate`s for the page:

| Situation | What happens |
|---|---|
| A connection that was open ≥ 10 s after its first snapshot ended normally (usually token expiry) | Reconnect after 2–5 s; the new request gets a fresh token because the handler refreshes a due token first |
| Other failures | Reconnect after a random delay between half the step and the step + 1 s, where the step is min(30 s, 2^n s): 2–3 s, 2–5 s, 4–9 s, 8–17 s, then 15–31 s. A connection that stayed open ≥ 10 s after its first snapshot resets n; a slow attempt that never delivered a snapshot does not |
| `RateLimited` | Wait 15–20 s (or the failure delay, if longer) |
| No event for 50 s (the Api sends a heartbeat after 20 s of silence) | Drop the connection and reconnect |
| `NotFound` | Stop: `Gone` (cancelled, expired or left) |
| `Unauthorized` | Stop: `SignedOut` (the auth state change also sends the page to login) |
| `Closed` after a `finished` snapshot | Stop: `Finished` |

Every delay is at least 2 s, which keeps one tab under the 30-openings-per-minute limit; with several tabs failing together, the growing delays keep the user under it. The random spread keeps clients cut off at once (an Api restart) from reconnecting in waves. Each connection starts with the current snapshot, so a reconnect is a full resync and there is no gap detection. `GamePage` restarts the watch when the route's game id changes and cancels it when disposed.

## Client game board (hex map, selection and commands)

A started game on `/games/{id}` shows `Features/Games/Components/GameTable`: the full-screen game view: the board (`Components/Board/GameBoard`, WebGL) fills the window and a top strip (title, connection, turn, End turn, Log, Full screen, Resign), the selection card with the unit's actions, the attack forecast beside its target, the log drawer and the notices (including the result once finished) float over it. Its layout, UI state (`GameUiService`), tokens, icons and shared components are the [game UI design system](game-ui.md). The client references `Govern.Core` for the view types (`PlayerView`, `UnitView`, `CityView`, commands), for the view-based previews (`GameEngine.ReachableTiles`/`AttackTargets`/`CheckFoundCity` over a `PlayerView`, [game-core](game-core.md#view-previews-for-the-client)) and for the game's `Ruleset`. `Features/Games/Services/RulesetService` embeds `rulesets/default/v2.json`, the same file the Api plays every game with, and parses it once; `GameTable` passes it to the previews and panels, which take unit names, stats and the build list from it ([game data](../game/game-data.md)). Fetching the ruleset each game is pinned to comes with Ruleset B, which is deferred ([ruleset B plan](../plans/deferred/ruleset-b-store-and-pin.md)). It never holds a `GameState` or runs `Execute`; the server stays authoritative (`LayerDependencyTests.Client_UsesCoreOnlyForViewsAndPreviews`).

| Part | What it does |
|---|---|
| `Board/GameBoard` | The WebGL board ([game UI](game-ui.md#game-board)): the view becomes a `BoardScene` in C# (`Helpers/BoardSceneBuilder`; tiles placed by `Helpers/HexLayout`, pointy-top, odd-r), the selection and lens a `BoardOverlay`, and `wwwroot/js/board3d` draws them and reports clicked tiles. It opens on the player's own units and cities with 3 hexes of room (`HexLayout.Around`), or on the whole map when they have none |
| `Screen/TopBar` | Your seat colour, "Turn n: your turn" or "opponent's turn" ("game over" once finished), End turn (your turn only), Resign with an inline confirm (`ConfirmButton`, allowed at any time) |
| `Screen/ActionBar` | For your selected unit: a hint for board moves and, for a unit with the `found_city` ability, Found city, disabled with the rule's message from `CheckFoundCity` |
| `Screen/SelectionCard` | The panel for the selection with the unit's actions (or a hint), collapsible |
| `Screen/NoticeStack`, `Screen/LogDrawer`, `Screen/AttackForecast` | The stale notice, Api error and result; the game log drawer; the forecast the board keeps beside its target |
| `UnitPanel` | The selected unit in its owner's seat colour, named from the ruleset ("Your Settler"); for your own unit a `StatTable` with movement left of the ruleset's movement, sight and production cost |
| `CityPanel` | The selected city; for your own city, what it produces (progress of the ruleset's cost, and a `ProgressBar` with the turns left at the ruleset's production per turn) and its Units, Buildings and Economy tabs (the Units tab has one Build button per ruleset unit with its cost, in the ruleset's order; details in [game UI](game-ui.md)). An enemy city shows its name only (its production is hidden in the view) |
| `GameOverPanel` | "You won!", "You lost." or "Nobody won.", how each player ended (won, resigned, eliminated), and a link back to `/games` |

### Board interactions

| Input | Result |
|---|---|
| Click a tile | `Helpers/BoardClickResolver.Resolve`: with your unit selected, a reachable tile sends `move_unit` and an attackable one sends `attack` (both from the Core previews, so only moves the server accepts are offered); otherwise the click selects the unit on the tile (clicking your selected unit again selects your city under it), else the city, else nothing. Enemy units and cities can be selected to inspect them. No command is built while it isn't your turn or the game is over |
| Drag (press and move more than 4 px, 10 for a finger) | Pans; a drag is never a click. Two fingers pinch to zoom and pan (`js/board3d/gestures.js`) |
| Wheel over the board | Zooms at the pointer; the page does not scroll |
| +, −, Fit buttons | Zoom at the centre by 1.2 (`GameBoard.ZoomStep`), or fit the whole map |

### Board scripts and vendored Three.js

The board (F15; the only board since F17) is drawn by ES modules in `wwwroot/js/board3d/` with **Three.js**, vendored in `wwwroot/lib/three/` (`three.module.js`, `three.core.js` and its MIT `LICENSE`, copied unchanged from the npm release, 0.186.1). Vendored files are served from the app's own origin, so the CSP stays as it is, and Blazor's import map fingerprints them like any other module. They pass `ClientSecurityTests` as they are (no HTML sinks, eval or browser storage); a library that doesn't is not vendored. `Features/Games/Services/Board3DInteropService` (scoped, resolved from `GameBoard`'s own scope as an `OwningComponentBase`, so it is disposed with the board and unmounts the renderer, also when the board goes away while the module is still loading) is the only door to them; the board itself is described in [game UI](game-ui.md#game-board).

### Sending commands (one at a time, stale handling)

- `GamesClient.SendCommandAsync` posts `{"expectedVersion": <view.version>, "command": {"type": …}}` to `POST /api/games/{id}/commands` ([games](games.md#sending-commands)). The command's `type` comes from Core's `[JsonPolymorphic]` `GameCommand`.
- `GameTable` sends **one command at a time**: while one is in flight the board ignores clicks and the buttons are disabled.
- 200: the answered game goes up to `GamePage` (`GameChanged`), which also keeps taking stream snapshots; whichever arrives last is shown, and both carry the full view.
- 409 `game.stale_version` (the view changed first, e.g. another tab acted): nothing was applied, so the table reads the game again (`GET`) and shows "The game changed before your move arrived, so nothing was applied. The board is up to date now."
- Any other error (a rule code such as `game.not_your_turn`, 404, 429, network) shows in `ApiErrorAlert` with the Api's message.
- A new view keeps the selection when the unit or city is still in it (`BoardClickResolver.Prune`), so a unit can move again at once; highlights are recomputed from the new view. A view that arrives from the stream (not the table's own command) clears the last error or stale notice.
- `GamePage` keys the table by game id, so moving to another game disposes the old table (cancelling its command), and it drops any answer for a game it has left.
- The stale notice can also follow a late stream push: if an older snapshot lands after a command's answer, the next command is based on it and gets `game.stale_version`. That costs one round trip and applies nothing.

Styles are global CSS files in `wwwroot/css/` (the client has no scoped `.razor.css`): `tokens.css` holds every colour, `board.css` the board and `game-ui.css` the screen zones, which stack map first on narrow screens ([game UI](game-ui.md)). The board has `touch-action: none`.

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
| `tests/Govern.ClientTests` (bUnit) | layout (body, navigation, sidebar toggle, signed-in and anonymous header and nav), `HomePage`/`NotFoundPage`, `App` with the real auth state (redirect to login, signed-in page), `AddClient` (URL validation, no bare `HttpClient`, resolvable services), `Auth/` (token store, JWT payload, auth state, refresh single flight, outcomes, backoff, compare-and-set and completion after the caller gave up, `RenewAt`, cross-tab user changes, the handler's bearer, origin check, retry and 503, the Web Lock module, `ReturnUrl`), sign-out with a due token sending the rotated tokens, `ApiError`, `UsersClient`, the login, register and profile pages (including `replace` after sign-in), `RedirectToLogin`, `SignOutButton`, `ApiErrorAlert`; games: `InviteLink`, `ReconnectBackoff`, `GamesClient` (every endpoint, the code only in the join body, problem mapping, the stream's events and end reasons, a refresh before opening), `GameWatchService` (delays, reset only after a real connection, rate limit, silence, heartbeats, final states, cancellation), the games, join (the fragment dropped at once and only filling the form, no join without a click, history, paste, validation) and game pages (lobby roles, live snapshots, start, cancel, gone, not found, id change dropping the old stream, dispose, dispose while loading, the board once started, snapshots and command answers updating it, game over), the lobby components and `ClipboardService`; the board: `HexLayout`, `BoardClickResolver` (move, attack, selection toggle, inspection, no commands off-turn or finished, pruning, highlights equal to the Core previews), `GamesClient.SendCommandAsync` (the body in the game format, every command read back by `GameSerializer`, a `GameSerializer` view read with `ApiJson.Options`, problems, 429, network), the scene, overlay and motion builders, `Board3DInteropService` and `GameScreenInteropService` (imports, failures, reduced motion), `GameBoard` (mount, the scene sent only for a new view version, overlay and motion, clicks from the renderer, zoom and Fit buttons, disabled, no WebGL) and `GameTable` (through `Support/FakeBoardRenderer`, which clicks tiles as the renderer does and reads the scene and overlay it was sent: move with the view version, stale reload and notice, rule errors, one command at a time, end turn, resign confirm, found city reason, production, enemy city and unit, selection pruning, highlights, lenses, animations, game over). `Support/` fakes the Api (`FakeApiHandler`, `SseResponse` for event streams), browser storage (`FakeBrowserStorage`) and time (`FakeTimeProvider`, with `Eventually` to wait for background work), and builds views by hand (`TestViews`) |
| `tests/Govern.ArchitectureTests` | CFT folders, page/component/client naming, `System.Net.Http`/`System.Net.ServerSentEvents` and `Microsoft.JSInterop` confinement, Core used for views and previews only, `.razor` size limits, and `ClientSecurityTests` (CSP directives, no unsafe script sources, headers, no route header overrides, `/.auth/*` blocked, `index.html` without external assets or inline scripts, no raw HTML or eval in `.cs`, `.razor` or `wwwroot` JavaScript, no HTML or code sinks in `wwwroot` JavaScript, `localStorage` only in `Auth/TokenStore.cs`, no secrets in appsettings), and `ClientContractTests` (a client DTO named like an Application contract has only that contract's properties, with the same shapes; `GetGameResponse` and `SendGameCommandRequest` must be found; `TokenPair` and the refresh body are paired explicitly with the sign-in and refresh contracts) |

## Running the client locally

```bash
dotnet run --project src/Govern.Api      # https://localhost:7294
dotnet run --project src/Govern.Client   # https://localhost:7042 (http://localhost:5114)
```

The dev server doesn't apply `staticwebapp.config.json`, so the CSP is only active on the Static Web App. After a release, `curl -I https://<static-web-app-host>` shows the headers, and DevTools must show no CSP violations.
