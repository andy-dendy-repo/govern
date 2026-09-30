---
title: Game UI design system
tags: [client, game-ui, design-system, layout, tokens, css, theme, dark-mode, icons, sprite, svg, board-layers, components, accessibility, keyboard, lenses]
code: [src/Govern.Client/Components/Game, src/Govern.Client/Features/Games/Components/Screen, src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Services/GameUiService.cs, src/Govern.Client/Features/Design/Pages/DesignPage.razor, src/Govern.Client/Helpers, src/Govern.Client/wwwroot/css, tests/Govern.ArchitectureTests/GameUiTests.cs]
updated: 2026-09-30
---

# Game UI design system

The game UI grows with every mechanic in the [design roadmap](../game/design-roadmap.md): terrain, units, buildings, trees, governors, seasons. The design system (backlog item G1) fixes the approaches once so each mechanic plugs into an existing slot instead of reshaping the screen. The rule file is `.claude/rules/game-ui.md`; the plan is [G1](../plans/done/G1-ui-design-system.md) (done).

**How it stays expandable:** every extension point is a registry or a slot, and a test fails when a new game thing lacks its UI part. A ruleset unit or terrain without an icon fails `GameSpriteTests`; a terrain without colours fails `GameUiTests`.

## Game screen layout (zones)

A started game (`Features/Games/Components/GameTable`) is a CSS grid with fixed zones (`wwwroot/css/game-ui.css`).

| Zone | Component | Holds | Grows with |
|---|---|---|---|
| Top bar | `Screen/TopBar` | Your seat colour, "Turn n: your turn", End turn, Resign (inline confirm) | treasury (G8), science and culture (G11), season (G14): one element each, before the actions |
| Map | `Board/GameBoard` | The hex board, pan and zoom, zoom buttons | map lenses (toolbar in this zone), new board layers |
| Action bar | `Screen/ActionBar`, floating over the bottom of the map | The selected own unit's actions (Found city, with the rule's reason when disabled) and a hint for board moves | one button per new ability |
| Side panel | `Screen/SidePanel` | Notices (stale command, Api error), the game result, the panel for the selection (`UnitPanel`, `CityPanel`) or a hint | one panel per new selection kind (tile, building) |
| Full-screen view | (added with its first user) | Large content: tech and culture trees (G11), the city screen, governors (G12) | |

The action bar floats over the bottom-left of the map, so a unit's actions sit next to the board without scrolling; it covers a few tiles in that corner, which the player reaches by panning. On screens narrower than 60rem the zones stack: top bar, map (with its action bar), side panel (map first, panels below).

## Interaction rules

| Rule | How |
|---|---|
| Select, then act | A click selects a unit or city; moves and attacks are clicks on highlighted tiles; other actions are buttons in the action bar or side panel |
| Exact numbers on demand | `NumberTooltip`: hover shows the title; the info button shows it on click, Enter, Space or tap (no long press); Esc closes it |
| Destructive actions confirm inline | `ConfirmButton`: the first click asks, a second click on the confirm button acts, Cancel or Esc backs out (Resign today) |
| No accidental actions | A drag on the board is never a click; commands are sent one at a time; a link never acts on its own |
| Keyboard | Every action is a button (Tab reaches it); Esc clears the selection (`GameUiService.Escape`). The screen takes keyboard focus on arrival and after each command (whose disabled buttons would drop it); an Esc handled by an open `ConfirmButton` or `NumberTooltip` stops there |

## UI state (GameUiService)

`Features/Games/Services/GameUiService` holds the UI state of one game screen: the selection (`BoardSelection`) and its highlights (`BoardHighlights`, from the Core previews). `GameTable` creates one per game and cascades it (`CascadingValue IsFixed`), so `SidePanel` and `ActionBar` read the selection instead of receiving it through parameter chains. It holds no game state: the view stays the server's. A new view calls `Refresh`, which drops a selection that is no longer visible. It also holds the active map lens ([map lenses](#map-lenses)); later UI state (an open full-screen view) is added here.

## Design tokens, themes and colours

All colours are CSS custom properties in `wwwroot/css/tokens.css` (`--gv-<group>-<name>`); `GameUiTests.Css_UsesColourValuesOnlyInTokens` rejects colour values in any other CSS file, and `Razor_WritesNoColourValues` rejects them in component attributes.

| Tokens | Use |
|---|---|
| `--gv-seat-1` … `--gv-seat-4` | One colour per seat in turn order (`PlayerView.Players`); classes `seat-1`… set `fill` and `color` (`Helpers/Seats`). More than four seats (F10) needs more tokens |
| `--gv-terrain-<id>`, `--gv-terrain-<id>-explored` | Visible and remembered shade per ruleset terrain. `board.css` maps each id with one `.terrain-<id>` rule (tiles and terrain icons) |
| `--gv-board-*`, `--gv-tile-*`, `--gv-overlay-*`, `--gv-selected`, `--gv-label*`, `--gv-icon-mark` | The board |
| `--gv-text`, `--gv-surface*`, `--gv-border`, `--gv-accent`, `--gv-danger` | Panels and components, built on Radzen's `--rz-*` theme variables |
| `--gv-space-1..3`, `--gv-radius`, `--gv-icon-size` | Spacing and shape |

**Light and dark:** the app follows the system setting. `index.html` loads Radzen's `material.css` for `prefers-color-scheme: light` and `material-dark.css` for dark, and `tokens.css` overrides the board tokens under the same media query. There is no switch in the UI yet.

**CSS files** (global, no scoped `.razor.css`): `tokens.css` (values), `app.css` (Blazor boot screen and error UI), `board.css` (the board, seats, terrain mapping), `game-ui.css` (the screen zones, components and the design gallery).

## Icon set (GameSprite)

`Components/Game/GameSprite.razor` holds every game icon as a `<symbol>`; the game screen renders it once (hidden) and every `<svg>` on the page draws an icon with `<use href="#id">`, so the board and the panels share one set, all self-hosted and CSP-safe (no external files, no raw markup).

| Symbol id | For |
|---|---|
| `unit-<id>` | Each ruleset unit (`Icons.Unit`) |
| `terrain-<id>` | Each ruleset terrain (`Icons.Terrain`): a hex swatch filled with the terrain colour plus a `.terrain-mark` drawing (trees, humps, peaks, waves; none for plains). The board draws it on every known tile that isn't the default terrain, where the hex blends into the tile and only the drawing shows, so terrain is never told by colour alone |
| `city` | Cities |
| `ui-movement`, `ui-sight`, `ui-production`, `ui-turns` | UI glyphs (`Icons` constants) |

Shapes sit in a 26×26 box centred on 0,0. The main shape takes its fill from the `<use>` (a seat or terrain class); `.icon-mark` draws the white mark inside unit symbols and `.icon-line` draws outlined glyphs in the text colour. `GameIcon` draws one icon at text size; it is decorative unless `Label` is set (then `role="img"` with that name). `GameSpriteTests` checks every ruleset unit and terrain and every `Icons` constant has a symbol.

## Board layers

The board's SVG groups render in the fixed `Features/Games/Enums/BoardLayer` order, each as `<g data-layer="…">`: terrain → rivers → improvements → borders → lens → highlights → cities → units. A layer with nothing to draw yet is not rendered. Lenses and highlights sit under the pieces so units stay readable. Clicks go to the terrain tiles; every layer above has `pointer-events: none`. `GameBoardTests.Render_Layers_FollowTheBoardLayerOrder` checks the order.

## Component catalog

Shared game components live in `Components/Game` (each with bUnit tests in `tests/Govern.ClientTests/Components/Game`); feature panels in `Features/Games/Components`.

| Component | Status | Use |
|---|---|---|
| `GameSprite`, `GameIcon` | built | The icon set and one icon |
| `StatTable` | built | Icon, label and value rows (`Models/StatRow`), with an optional detail tooltip; `UnitPanel` shows movement left, sight and cost |
| `NumberTooltip` | built | A number with its exact value and origin (hover, focus, tap) |
| `ProgressBar` | built | Progress towards a cost with "n turns left" at the current rate ("Complete" once reached, "Never at this rate" without income); `CityPanel` shows production |
| `CostLabel` | built | Icon and amount; each Build button shows the unit's production cost |
| `ConfirmButton` | built | Inline confirm for destructive actions (Resign) |
| `YieldList`, `ResourceBadge` | planned by G5/G8 | Yield and resource rows and badges |
| `QueueList` | planned by G8/G10 | A reorderable build queue |
| `TreeView` | planned by G11/G12 | Tech, culture and governor skill trees in a full-screen view |
| `LensToolbar`, `BoardLens` and `IMapLens` | built (G4) | Toggleable map overlays, one at a time ([map lenses](#map-lenses)); the first is movement cost. Later lenses: resources (G5), rivers (G6), territory (G7), yields and loyalty (G8), seasons (G14) |

## Map lenses

A lens marks every known tile with one kind of information, instead of cluttering the base map.
- **A lens** is a sealed `Features/Games/Lenses/XxxLens` implementing `IMapLens` (id, name, toolbar icon, and `MarkFor(TileView)` → a `Models/LensMark` with a label, a tint class and a sentence), registered in `DependencyInjection` as `IMapLens`. `NamingConventionTests.ClientMapLenses_AreSealedLensesImplementingIMapLens` and `FolderStructureTests` (`*Lens.cs` only) keep the folder to that.
- **State:** `GameUiService.ActiveLens`, changed by `ToggleLens` (one on at a time; toggling the active one turns it off). Escape clears the selection, not the lens.
- **Toolbar:** `Components/Board/LensToolbar` in the map zone's top-left corner, one toggle button per registered lens (`aria-pressed`, keyboard reachable). It reads the cascaded `GameUiService`, so it renders nothing outside a game screen.
- **Drawing:** `Components/Board/BoardLens` draws the active lens in the `BoardLayer.Lens` slot: per marked tile a tinted hex (`.lens-mark.<class>` in `board.css`, colours from `--gv-lens-*` tokens), a label and a `<title>` with the sentence. Unexplored tiles are never marked.
- **Movement cost** (`MovementCostLens`): each explored or visible tile shows its terrain's movement cost (tint `cheap` for 1, `costly` above), and ✕ (`blocked`) where land units can't go.

## Design gallery (/design)

`Features/Design/Pages/DesignPage` at `/design` shows every catalog component with sample data, the icon set (from the ruleset) and the seat colours. It renders only in the Development environment (`IWebAssemblyHostEnvironment.IsDevelopment()`); elsewhere it shows the not-found text, and it has no navigation link. Look there before building a new component, and add every new component to it.

## Performance

The terrain renders once per view (`BoardTerrain.ShouldRender`) and pieces only when the view, selection or highlights change; panning and zooming change one `transform`. Maps are 20×14 by default and at most 40×40 (`map.maxSize` in the ruleset), which renders smoothly; measure the largest offered size again before moving terrain to a cached layer or a canvas.

## Adding UI for a new mechanic

1. A new ruleset unit or terrain: add its `<symbol>` to `GameSprite` (and, for a terrain, its two tokens in `tokens.css` and its `.terrain-<id>` rule in `board.css`). The tests name what is missing.
2. A new number or list on a panel: use `StatTable`, `ProgressBar`, `CostLabel`; add a planned component (`YieldList`, `QueueList`, `TreeView`) to `Components/Game` when its G item needs it, with tests and a gallery entry.
3. A new action: a button in `ActionBar` (unit) or the panel (city), disabled with the rule's reason; a destructive one uses `ConfirmButton`.
4. A new selection kind: its panel in `SidePanel`, reading `GameUiService`.
5. New map content: its `BoardLayer` slot. Information a player toggles on and off: a new `IMapLens` ([map lenses](#map-lenses)).
6. Update this document's catalog.
