---
name: client-checks
description: Govern Blazor client review checks - arch-rule exemptions, configure-client.sh, board/page traps, G1 game-UI traps, F15 3D board (Three.js disposal, mount race)
metadata:
  type: project
---

Review 5 (2026-09-27, feature/blazor-client-foundation, follow-up #10a). Facts I verified, so don't re-derive them:
- .NET 10 standalone WASM: the environment is baked in at build/publish (`WasmApplicationEnvironmentName`; Production for publish). The `Blazor-Environment` header is ignored. The boot config lists appsettings files without integrity hashes, so post-publish edits of `appsettings.Production.json` are safe.
- `Router.NotFoundPage` renders through the app's `Found` fragment (`RouteView` with DefaultLayout), so a page without `@layout` still gets `MainLayout`.
- `configure-client.sh`: the Python hash equals the browser's (universal newlines match HTML CR/LF normalisation). Re-runs are idempotent. A trailing slash or empty host fails. Publish emits `.br/.gz` for the config and appsettings, and the script deletes them.
- Arch rule gaps I reported: the HttpClient rule regex `^Govern\.Client\.(?!…)` exempts the root namespace (needed for `AddHttpClient` in DependencyInjection, but undocumented). The JSInterop rule exempts only the root `Services`, not `Features/*/Services`.

**Why:** Plan 7 (auth pages, #10b) lands in these folders next and relies on these rules.
**How to apply:** when reviewing Plan 7, check that typed clients don't leak `HttpRequestException` to pages (pages can't depend on System.Net.Http), that refresh in `AuthorizationMessageHandler` doesn't resolve `UsersClient` (DI cycle), and that `localStorage` is touched only in `Auth/TokenStore`. Related: [[convention-sweep-gaps]].

Review 7 (2026-09-29, feature/client-lobby, Game Plan 4). Traps worth re-checking in later client/game pages (Plan 5 board reuses them):
- A page that awaits an Api call in `OnParametersSetAsync`/`OnInitializedAsync` and THEN starts a background loop (SSE watch) must check a disposed flag / lifetime token after the await; otherwise a page left mid-GET leaks a live stream (holds 1 of the Api's 4 per-user stream permits, reconnects forever).
- Stop/restart helpers that null fields after `await` race with `DisposeAsync` (Blazor can call SetParametersAsync again or dispose while the previous async lifecycle call is pending): swap fields to locals first.
- `GamesClient.WatchAsync` contract is "only OCE on cancel": with `IsNetworkFailure` now returning false once the token is cancelled, a non-OCE exception that races the silence-timer cancel escapes; services can't catch HttpRequestException (arch rule), so the mapping must live in the client.
- Verified: Blazor ignores `StateHasChanged` on a disposed component (no crash, so leaks stay silent); `Permissions-Policy` doesn't block clipboard-write; per-user stream limiter = concurrency(4) then fixed window(30/min), concurrency rejects don't consume the window.

Review 8 (2026-09-29, feature/client-board, Game Plan 5b). Traps for later board/page work:
- ComponentBase renders ONCE at the first yield of OnParametersSetAsync and then only at completion. GamePage's id change awaits StopWatchingAsync (yields) BEFORE `_game = null`, so the old game's children (GameTable) stay mounted during the next GET and are REUSED for the new game (stale `_error`/`_stale`, no refit, in-flight command's GameChanged overwrites the new game). Look for `@key` on per-game children and an id check in child→page callbacks (OnGameChanged/OnStarted).
- Board ShouldRender guards are load-bearing: EventCallback params always count as changed, so every parent render calls SetParametersAsync on BoardTerrain/BoardPieces.
- Client tests compare records holding ImmutableHashSet/ImmutableArray by reference; passes only via `.Empty` singletons.
- Verified: Core previews (ViewPreviewCalculator) return empty off-turn/finished/non-own unit and exclude the unit's own tile; arch rule Client_UsesCoreOnlyForViewsAndPreviews names real types.

Review 9 (2026-09-30, G1 game UI design system, phases 1-4). Traps for later game-UI work:
- `GameUiService` is cascaded with `IsFixed=true`: children (SidePanel, ActionBar) update only because GameTable re-renders them (View/EventCallback params always count as changed). A future child with only primitive params, or one that calls `Ui.Select` itself, needs the parent's `Changed -> StateHasChanged` path; check it.
- Esc is a `@onkeydown` on `div.game-screen tabindex=-1`: works only while focus is inside it (not on load, not after a focused button becomes disabled); every keydown re-renders GameTable; Esc inside ConfirmButton/NumberTooltip bubbles and clears the selection instead.
- `.game-icon { fill: currentColor }` (game-ui.css, loaded after board.css) beats `.seat-N { fill }` at equal specificity; it works only because `.seat-N` also sets `color`. Seat 0 (unknown) and seat >4 have no colour.
- `GameUiTests` colour regexes catch hex and functional colours only, not named colours (`white`, `red`).

Review 10 (2026-09-30, G4 terrain + lenses). Check on later board work: new tokens are often added only to the light `:root` block (G4 `--gv-terrain-mark`, `--gv-lens-*`), so a dark-on-dark mark ends up on dark explored tiles; `<title>` inside a layer with `pointer-events: none` never shows as a hover tooltip; param-less children (LensToolbar) re-render only from their own handlers, not from the parent.

Review 11 (2026-10-01, F11b BoardAnimationService). Traps: GameWatchService doesn't dedupe snapshots by view version, and GamePage replaces `_game` with every SSE snapshot, so a component that reacts to `!ReferenceEquals(View, _view)` fires twice per own command (response + SSE echo); anything "ended by a newer view" gets cut by the echo. Timer-driven `StateHasChanged` on GameTable re-renders TopBar/ActionBar/SidePanel/GameLog (no ShouldRender) every step. In WASM, `CancellationTokenSource.Cancel` runs the cancelled Delay's continuation inline, so stale-step races after Finish are not reachable there.

Review 12 (2026-10-02, F15 step 1, 3D board / Three.js 0.186.1 vendored in wwwroot/lib/three, byte-identical to npm). Traps for later F15 steps:
- `WebGLRenderer.dispose()` does NOT remove its 'dispose' listeners from geometries/materials it used, and does not release the context. Module-level shared geometries/materials (terrain.js) therefore keep every old renderer reachable; each 2D/3D toggle leaks a GL context. Check that unmount disposes shared resources (or makes them per-board) and calls `renderer.forceContextLoss()`.
- `Board3DInteropService` mount is async (2 MB import); a component disposed before `MountAsync` returns leaves a live board + DotNetObjectReference. Look for a disposed flag checked after the await.
- Transient IAsyncDisposable services injected into components are tracked by the WASM app scope until exit (never collected).
- A pre-existing `GameUiService.Focus` is replayed when the 3D board mounts (2D refits after it, 3D focuses after Fit).
- `ReferenceEquals(View, _drawn)` rebuilds the whole scene twice per own command (SSE echo, see Review 11); compare `View.Version`.

Review 13 (2026-10-02, F15 step 2, rivers/bridges/features/borders in 3D). Traps for later F15 steps (pieces, lenses):
- Heights in C# (`TerrainReliefs.Surface`) ignore the JS per-tile jitter (±0.02, `tileTop`); every layer lift must exceed 0.02. Unexplored tiles report height 0.02, so "lower of both sides" logic sinks things at the fog frontier; lake tiles' `Height` (0.03) sits under the 0.1 water, so use `Surface`, not `For(tile).Height`.
- Layer modules keep module-level geometries/materials and must be added to Board3D.dispose's disposeShared list; per-build InstancedMesh/geometry freed by `shapes.disposeLayer`.
