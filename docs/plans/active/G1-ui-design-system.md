---
title: "Plan: G1 UI design system"
tags: [plan, game-ui, design-system, client, tokens, icons, components, lenses]
code: [src/Govern.Client, docs/features/game-ui.md, .claude/rules/game-ui.md]
updated: 2026-09-30
---

# Plan: G1 UI design system

**Status:** phases 1–4 implemented on branch `claude/project-thread-pokkxe` (PR into `stage`); phase 5 (map lenses) waits for G4 terrain. The result is documented in [game UI](../../features/game-ui.md). What changed against this plan is under "Decisions made during implementation" at the end.

## Context
Every upcoming game item (terrain, units and combat, buildings, trees, governors, seasons) adds screens, panels, numbers and icons. Today the game UI is one `GameTable` with a board and a stack of Radzen panels, colours hard-coded in `wwwroot/css/app.css`, board symbols inline in `GameBoard.razor`, and selection state held in `GameTable`. G1 fixes the approaches once (layout, interaction, visual language, components, UI state) and builds them so that each new mechanic plugs in instead of reshaping the screen. UI only: no Core, ruleset or Api change.

The guiding idea for "expandable": **each extension point is a registry or a slot with a test that fails when a new game thing lacks its UI part.** Adding a unit to the ruleset without an icon fails a test; adding a lens is one class plus one registration; adding a panel for a new selection kind is one component in the side-panel slot.

## Scope
- In:
  - Design approaches written down (`docs/features/game-ui.md`) and enforced (`.claude/rules/game-ui.md` + architecture and client tests).
  - Design tokens (CSS custom properties) for seat colours, terrain palette, board layers, spacing, light and dark.
  - The game screen zones (top bar, map, right panel, bottom action bar, full-screen view slot) and one per-game UI state service.
  - One self-hosted SVG icon sprite, used by the board and the panels, keyed by ruleset ids.
  - The first shared components that today's game already needs, each with bUnit tests.
  - The map-lens framework (registry + toolbar), shipped with the first lens when G4 adds real terrain.
  - A living component gallery page (Development only) that shows every component with sample data.
- Out (deferred, added by the G item that first needs them): `YieldList`, `ResourceBadge` (G5/G8), `QueueList` (G8/G10), `TreeView` (G11/G12), the season and treasury top-bar slots (G14, G8), lenses for resources, territory, yields, loyalty, rivers, seasons. A theme switcher in the UI (tokens support it; the toggle waits). Canvas rendering (only if the 60×40 measurement says so).

## Affected areas
- `Govern.Client`
  - `wwwroot/css/`: split `app.css` into `tokens.css` (variables), `board.css`, `game-ui.css`; `app.css` keeps the Blazor boot styles. Linked from `wwwroot/index.html`.
  - `wwwroot/icons/game.svg`: the sprite (one `<symbol>` per unit id, terrain id, city, and UI glyph).
  - `Components/Game/`: shared game components (`GameIcon`, `StatTable`, `ProgressBar`, `CostLabel`, `NumberTooltip`, `ConfirmButton`).
  - `Features/Games/Components/Screen/`: `GameScreen` (zones), `TopBar`, `SidePanel`, `ActionBar`, `FullScreenView`.
  - `Features/Games/Components/Board/`: `GameBoard` reads symbols from the sprite; `BoardLayers` enforces the layer order; `BoardLens` renders the active lens.
  - `Features/Games/Services/GameUiState.cs` (selection, active lens, open full-screen view; raises `Changed`).
  - `Features/Games/Lenses/`: `IMapLens` + lenses, registered in `DependencyInjection.cs`. (New folder, whitelisted in `FolderStructureTests`.)
  - `Features/Design/Pages/DesignPage.razor` (`/design`, Development only).
- Existing code to reuse: `GameTable.razor(.cs)` (selection, command sending, stale handling move into `GameScreen` + `GameUiState`), `TurnPanel`, `UnitPanel`, `CityPanel`, `GameOverPanel` (become side-panel and action-bar content), `BoardSelection`, `BoardHighlights`, `HexLayout`, `RulesetService` (names and stats), the resign confirm in `TurnPanel` (becomes `ConfirmButton`).
- `tests/Govern.ClientTests`, `tests/Govern.ArchitectureTests`, `docs/features/game-ui.md`, `docs/features/client.md`, `docs/README.md`, `.claude/rules/game-ui.md`, `.claude/rules/blazor-client.md`, `CLAUDE.md` (rules table).

## Steps
Each phase is its own branch off `stage` and PR into `stage`, and leaves the game playable.

### Phase 1: Foundations (approaches, tokens, rule)
1. Write `docs/features/game-ui.md`: the decided approaches from the roadmap's G1 table (layout zones, "select, then act", tooltips with exact numbers, inline confirm for destructive actions, lenses, visual language, layer order, UI state, performance, accessibility), plus the component catalog with status (built / planned by Gx).
2. Add `.claude/rules/game-ui.md` (path-scoped to `src/Govern.Client/**`): where each kind of game UI goes, "no colour literals outside `tokens.css`", "icons only through `GameIcon` / the sprite", "every ruleset id has an icon", "panels read state from `GameUiState`, not through long parameter chains", "a new mechanic adds its component to the catalog and the gallery".
3. Create `wwwroot/css/tokens.css`: `--gv-seat-1..n`, `--gv-terrain-<id>-visible|explored`, `--gv-overlay-reachable|attackable`, `--gv-selected`, `--gv-board-bg`, spacing and radius, built on Radzen's `--rz-*` variables where they overlap. Light and dark values via `prefers-color-scheme` (the Radzen theme follows it through `RadzenTheme`, verified against Radzen 11.5 docs first).
4. Move the board styles to `board.css` using only tokens; seat colours come from the seat index (`seat-1`, `seat-2`) instead of `own` / `enemy`, with the viewer's own pieces still marked.
5. Architecture test: no hex / `rgb(` colour literals in `wwwroot/css/*.css` except `tokens.css`, and none in `.razor` `style` attributes.

### Phase 2: Game screen layout and UI state
1. `GameUiState` (scoped per `GameScreen`, created by it and passed as a cascading value, since the client has one DI scope): selection, highlights, active lens id, open full-screen view; one `Changed` event; pure state transitions testable without a renderer.
2. `GameScreen` replaces `GameTable`: CSS grid zones `top-bar / map + side-panel / action-bar`, wrapping on narrow screens (map first, panels below). Command sending, the one-at-a-time rule and stale handling move over unchanged.
3. `TopBar`: turn and whose turn, End turn; slots for later counters (treasury, science, culture, season) as a `RenderFragment` list so G8/G11/G14 add one entry each.
4. `SidePanel`: picks the panel for the selection kind (unit, city, later tile) from a small map of kind → component, so a new selection kind is one entry. `UnitPanel`/`CityPanel` move in.
5. `ActionBar`: the selected unit's actions (Found city today, Attack hint), each keyboard-reachable; Resign moves to a menu with `ConfirmButton`.
6. `FullScreenView` slot (empty today; G11 trees and the city screen use it). Esc closes it and clears the selection.
7. Keyboard: Esc deselects, Enter ends the turn when nothing is focused, Tab reaches every action.

### Phase 3: Visual language (icons, layer order)
1. `wwwroot/icons/game.svg`: move `#hex`, `#city`, `#settler`, `#soldier` out of `GameBoard.razor`; add UI glyphs (movement, sight, production, turns). Same-origin file, so CSP stays as is (`img-src 'self'`); checked in the browser with Playwright that external `<use href>` loads under the CSP.
2. `GameIcon` component (`Id`, `Size`, `Label` for `aria-label`), used in panels; the board uses the same sprite through `<use href="icons/game.svg#id">`.
3. `BoardLayers`: one component that renders fixed `<g>` layers in the documented order (terrain → rivers → improvements → borders → cities → units → overlays); empty layers render nothing. A test asserts the order.
4. Client test: every unit id and terrain id in `rulesets/default/v1.json` has a `<symbol>` in the sprite (reads the embedded ruleset and the sprite file). This is the check that makes a new unit or terrain fail until it has an icon.
5. Measure a 60×40 map in the browser (Playwright, pan and zoom); record the result in `game-ui.md`. Move terrain to a cached layer only if it is slow.

### Phase 4: Component catalog v1 and gallery
1. `StatTable` (label, icon, value rows; used by `UnitPanel` for movement, sight, cost).
2. `ProgressBar` with "n turns left" (used by `CityPanel` for production: progress, cost, per-turn from the ruleset).
3. `CostLabel` (icon + amount; production only today, resources later).
4. `NumberTooltip`: hover and focus show the exact number and where it comes from; on touch a tap on the info icon opens it (no long-press dependency).
5. `ConfirmButton`: inline confirm for destructive actions (replaces the resign code in `TurnPanel`).
6. `DesignPage` at `/design`, routed only when `IWebAssemblyHostEnvironment.IsDevelopment()`: every catalog component with sample data built from `TestViews`-like fixtures in the client. Linked from `game-ui.md` as the place to look before building a new component.

### Phase 5: Map lenses (with G4 terrain)
1. `IMapLens` (id, name, icon, `TileClass(TileView)` or a per-tile label) and a registry filled in `DependencyInjection.cs`.
2. `LensToolbar` in the map zone (toggle, one active at a time, state in `GameUiState`); `BoardLens` renders it in the overlays layer.
3. First lens: movement cost (from `TerrainDefinition.MovementCost`), shipped with or right after G4 so it shows real differences. Starts after G4's terrain lands; the framework can merge earlier behind an empty toolbar if G4 is late.

## Tests
- Unit: none (client only; Utils, Core and Domain are untouched).
- Integration: none (no Api change).
- Client (bUnit), per component: renders what the player sees; `GameUiState` transitions (select, deselect, lens toggle, open/close view); `GameScreen` keeps today's behaviour (the existing `GameTable` tests move over: one command at a time, stale version, finished game); `ActionBar` keyboard reachability; `ConfirmButton` needs two clicks; `ProgressBar` turns-left maths; every ruleset id has a sprite symbol; `BoardLayers` order; `DesignPage` is not routed outside Development.
- Architecture: no colour literals outside `tokens.css`; `Features/Games/Lenses` whitelisted and holds only `IMapLens` implementations named `XxxLens`; `Components/Game` holds Razor components only (existing rule covers it).

## Docs & rules
- `docs/features/game-ui.md` (new), index in `docs/README.md`; `docs/features/client.md` and `docs/features/games.md` updated for the screen and CSS files.
- `.claude/rules/game-ui.md` (new); `.claude/rules/blazor-client.md`: CSS files and the icon sprite replace "styles in app.css" and "symbols in GameBoard"; `CLAUDE.md` rules table lists `game-ui`.
- Backlog: G1 becomes `planned`, then `in progress` per phase PR. The G items that need deferred components (G5, G8, G10, G11, G12, G14) get a line "adds <component> to the G1 catalog" in the roadmap.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` green per phase, summary shown.
- Playwright against the running client (Api + client locally in the cloud env): create a game, play a turn, select unit and city, found a city, resign with confirm; screenshots in light and dark; no CSP errors in the console; narrow viewport wraps map first.
- Phase 3: 60×40 map pans and zooms smoothly; result noted in `game-ui.md`.

## Risks & open questions
- **Theme (default picked):** follow the system light/dark setting, no switch in the UI yet. Alternative: dark-only for the game screen.
- **Gallery (default picked):** Development only, not shipped to players.
- **CSS (default picked):** a few global CSS files with tokens, no scoped `.razor.css`, matching today's approach (the rule already says board styles live in global CSS).
- External `<use href>` to a sprite file inside SVG must load under the CSP and in WebKit; verified in phase 3 before the board switches. Fallback: the sprite is inlined once in `GameScreen` from the same file.
- Phase 5 depends on G4 (C1) for a useful first lens.

## Decisions made during implementation

- **One branch, one PR.** Phases 1–4 are separate commits in one PR into `stage` (the owner retired `gamecore` on 2026-09-30).
- **`GameTable` kept its name** and became the game screen, instead of a new `GameScreen`, so the page and its tests keep one entry point. Its zones are `Screen/TopBar`, `Screen/ActionBar`, `Screen/SidePanel`; `TurnPanel` was removed (its content is in `TopBar`, Resign uses `ConfirmButton`).
- **The UI state is `GameUiService`** (the client's `Services` folders require the `Service` suffix). It holds the selection and highlights only; the lens and open-view state are added with their first users.
- **The icon set is a Razor component (`Components/Game/GameSprite`)**, not an external `icons/game.svg`: same-document `<use href="#id">` needs no CSP or cross-browser check for external SVG references, the document's CSS styles the marks, and bUnit can check the symbols against the ruleset. Symbol ids are namespaced (`unit-<id>`, `terrain-<id>`, `ui-<name>`, `city`).
- **Board layer order:** lenses and highlights sit under cities and units (terrain → rivers → improvements → borders → lens → highlights → cities → units), so pieces stay readable; the roadmap listed overlays last.
- **Seat colours** come from the turn order (`seat-1`, `seat-2`), replacing the own/enemy colours; `own` and `enemy` classes stay as markers.
- **Light and dark:** Radzen `material.css` and `material-dark.css` are loaded by `prefers-color-scheme` media queries in `index.html`; no theme service or switch.
- **Deferred to their first user:** the `FullScreenView` slot (G11 trees, the city screen) and an Enter shortcut for End turn (a keydown on the screen can't tell whether a button has focus). Esc clears the selection.
- **Terrain palette is enforced by an architecture test** (`GameUiTests`: tokens and a `.terrain-<id>` rule per ruleset terrain), and icons per ruleset id by `GameSpriteTests`.
