---
title: Game UI design system
tags: [client, game-ui, tabs, city-tabs, upkeep, resource-cost, supply, building-health, design-system, layout, tokens, css, theme, dark-mode, icons, sprite, svg, board-layers, rivers, bridges, improvements, buildings, builders, territory, borders, city-stock, yields-lens, bridge-mode, components, accessibility, keyboard, lenses, health-bar, combat-forecast, game-log, animations, effects, reduced-motion]
code: [src/Govern.Client/Components/Game, src/Govern.Client/Features/Games/Components/Screen, src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Services/GameUiService.cs, src/Govern.Client/Features/Games/Services/BoardAnimationService.cs, src/Govern.Client/Features/Games/Components/GameLog.razor, src/Govern.Client/Features/Design/Pages/DesignPage.razor, src/Govern.Client/Helpers, src/Govern.Client/wwwroot/css, tests/Govern.ArchitectureTests/GameUiTests.cs]
updated: 2026-10-01
---

# Game UI design system

The game UI grows with every mechanic in the [design roadmap](../game/design-roadmap.md): terrain, units, buildings, trees, governors, seasons. The design system (backlog item G1) fixes the approaches once so each mechanic plugs into an existing slot instead of reshaping the screen. The rule file is `.claude/rules/game-ui.md`; the plan is [G1](../plans/done/G1-ui-design-system.md) (done).

**How it stays expandable:** every extension point is a registry or a slot, and a test fails when a new game thing lacks its UI part. A ruleset unit or terrain without an icon fails `GameSpriteTests`; a terrain without colours fails `GameUiTests`.

## Game screen layout (zones)

A started game (`Features/Games/Components/GameTable`) is a CSS grid with fixed zones (`wwwroot/css/game-ui.css`).

| Zone | Component | Holds | Grows with |
|---|---|---|---|
| Top bar | `Screen/TopBar` | Your seat colour, "Turn n: your turn", the resources in all your cities (one `CostLabel` per ruleset resource, the sum of your cities' stock), on your turn "n units can still move" (`.next-unit`: own units with movement left and no job, from `GameUiService.IdleUnits`; a click selects the next one, wrapping round), End turn, Resign (inline confirm) | season (G14): one element each, before the actions |
| Map | `Board/GameBoard` | The hex board, pan and zoom, zoom buttons. It opens fitted to your units and cities with 3 hexes of room (`HexLayout.Around`), or the whole map when you have none; Fit shows the whole map. The lens toolbar stops short of the zoom buttons and wraps on narrow boards | map lenses (toolbar in this zone), new board layers |
| Action bar | `Screen/ActionBar`, floating over the bottom of the map | The selected own unit's actions (Found city; Build <improvement> for each improvement that fits the unit's terrain; Build bridge, which turns on bridge mode; each with the rule's reason when disabled) and a hint for board moves or the unit's job | one button per new ability |
| Side panel | `Screen/SidePanel` | Notices (stale command, Api error), the game result, the panel for the selection (`UnitPanel`, `CityPanel` with its Units, Buildings and Economy tabs), the combat forecast of a pending attack (`CombatForecastPanel`) or a hint, and the game log at the bottom ([game log](#game-log-in-the-side-panel)) | one panel per new selection kind (tile, building) |
| Full-screen view | (added with its first user) | Large content: tech and culture trees (G11), the city screen, governors (G12) | |

Above the zones, a started game's page shows only a small title and the connection badge (no player list: the top bar has your colour and whose turn it is), so the board starts high. The top bar, side panel and action bar are cards (raised surface, border, radius). The action bar floats over the bottom-left of the map, so a unit's actions sit next to the board without scrolling; it covers a few tiles in that corner, which the player reaches by panning. On screens narrower than 60rem the zones stack: top bar, map (with its action bar), side panel (map first, panels below).

## Interaction rules

| Rule | How |
|---|---|
| Select, then act | A click selects a unit or city; moves are clicks on highlighted tiles. An attack on a unit takes two steps: a click on the target marks it and shows the combat forecast, only the forecast's Attack button attacks (so a double-click or double-tap never attacks unseen), Cancel or Esc backs out. Capturing an undefended city is one click (nothing to forecast). Other actions are buttons in the action bar or side panel |
| Exact numbers on demand | `NumberTooltip`: hover shows the title; the info button shows it on click, Enter, Space or tap (no long press); Esc closes it |
| Destructive actions confirm inline | `ConfirmButton`: the first click asks, a second click on the confirm button acts, Cancel or Esc backs out (Resign today) |
| No accidental actions | A drag on the board is never a click; commands are sent one at a time; a link never acts on its own |
| Bridge mode | Build bridge (`GameUiService.ToggleBridge`) marks the neighbours the unit can bridge to (`BoardHighlights.Bridgeable`, from `GameEngine.BridgeTargets`, dashed `overlay bridgeable`); a click on one sends the bridge, any other click, the button again or Esc leaves the mode |
| Keyboard | Every action is a button (Tab reaches it); Esc cancels a pending attack or bridge mode, else clears the selection (`GameUiService.Escape`). The screen takes keyboard focus on arrival and after each command (whose disabled buttons would drop it); an Esc handled by an open `ConfirmButton` or `NumberTooltip` stops there |

## UI state (GameUiService)

`Features/Games/Services/GameUiService` holds the UI state of one game screen: the selection (`BoardSelection`) and its highlights (`BoardHighlights`, from the Core previews). `GameTable` creates one per game and cascades it (`CascadingValue IsFixed`), so `SidePanel` and `ActionBar` read the selection instead of receiving it through parameter chains. It holds no game state: the view stays the server's. A new view calls `Refresh`, which drops a selection that is no longer visible, a pending attack target (`BoardSelection.Target`) the unit can no longer fight, and bridge mode (`BoardSelection.Bridge`) with nothing left to bridge. It also holds the active map lens ([map lenses](#map-lenses)); later UI state (an open full-screen view) is added here.

## Design tokens, themes and colours

All colours are CSS custom properties in `wwwroot/css/tokens.css` (`--gv-<group>-<name>`); `GameUiTests.Css_UsesColourValuesOnlyInTokens` rejects colour values in any other CSS file, and `Razor_WritesNoColourValues` rejects them in component attributes.

| Tokens | Use |
|---|---|
| `--gv-seat-1` … `--gv-seat-4` | One colour per seat in turn order (`PlayerView.Players`); classes `seat-1`… set `fill` and `color` (`Helpers/Seats`). More than four seats (F10) needs more tokens |
| `--gv-terrain-<id>`, `--gv-terrain-<id>-explored` | Visible and remembered shade per ruleset terrain. `board.css` maps each id with one `.terrain-<id>` rule (tiles and terrain icons) |
| `--gv-board-*`, `--gv-tile-*`, `--gv-overlay-*`, `--gv-selected`, `--gv-label*`, `--gv-icon-mark`, `--gv-health-*` | The board (`--gv-overlay-target` marks a pending attack target, `--gv-health-*` the health bar under a hurt unit) |
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
| `improvement-<id>`, `building-<id>`, `resource-<id>` | Each ruleset improvement (outlined lines, drawn small in the tile's upper left by the board), building and resource (`Icons.Improvement`, `Icons.Building`, `Icons.Resource`) |
| `ui-movement`, `ui-sight`, `ui-production`, `ui-health`, `ui-strength`, `ui-turns`, `ui-bridge` | UI glyphs (`Icons` constants) |

Shapes sit in a 26×26 box centred on 0,0. The main shape takes its fill from the `<use>` (a seat or terrain class); `.icon-mark` draws the white mark inside unit symbols and `.icon-line` draws outlined glyphs in the text colour. `GameIcon` draws one icon at text size; it is decorative unless `Label` is set (then `role="img"` with that name). `GameSpriteTests` checks every ruleset unit, terrain, improvement, building and resource and every `Icons` constant has a symbol.

## Board layers

The board's SVG groups render in the fixed `Features/Games/Enums/BoardLayer` order, each as `<g data-layer="…">`: terrain → rivers → improvements → borders → lens → highlights → cities → units → effects. A layer with nothing to draw yet is not rendered. Lenses and highlights sit under the pieces so units stay readable. Clicks go to the terrain tiles; every layer above has `pointer-events: none`. `GameBoardTests.Render_Layers_FollowTheBoardLayerOrder` checks the order.

## Game log in the side panel

`Features/Games/Components/GameLog` lists the player's log (`GetGameResponse.Log`, [games](games.md#game-log)) at the bottom of the side panel: one "Turn n" group per round, newest first, and in each round the newest line on top. `Helpers/GameLogFormatter` turns an event into one sentence with ruleset names and seats ("Your Archer hit Seat 2's Soldier: 22 damage, 6 taken", "An unseen Seat 2 Archer hit your Soldier"); `.yours` lines (your own actions) carry the accent border. A line with a tile has a Show button: it centres the board on the tile (`GameUiService.Focus`, `GameBoard.Focus`) and selects your unit or city there. The header button collapses the log (`GameUiService.LogOpen`, open by default, `aria-expanded`).

## Board animations

`Features/Games/Services/BoardAnimationService` (F11b, one per `GameTable`) plays the new log entries of each newer view on the board, in order. The view itself is applied at once, so panels and buttons never wait; only `BoardMotion` (unit positions, ghosts, effects) changes while it plays.

| Event | On the board |
|---|---|
| `unit_moved` | The unit slides tile by tile (`Step`, 120 ms a tile; a CSS `transform` transition on `.unit-at`); a unit no longer in the view walks as a ghost |
| `unit_attacked` | The attacker lunges a third of the way and back (`Lunge`, 160 ms), then both damage numbers float up (`.effect.damage`) |
| `unit_destroyed` | A ghost of the unit fades (`.ghost.fading`) |
| `city_founded`, `city_captured`, `improvement_built`, `bridge_built` | The tile pulses (`.effect.pulse`) |

- Each entry plays once (by `sequence`); the log the game opens with plays nothing.
- A newer view or a tile click finishes everything at once, so input is never blocked.
- `prefers-reduced-motion: reduce` (read through `BoardInteropService.PrefersReducedMotionAsync` and in CSS): units don't walk or lunge, and effects only fade.
- Effects are their own top layer (`BoardEffects`, `effects`); ghosts sit in the units layer without the `unit` class.

## Component catalog

Shared game components live in `Components/Game` (each with bUnit tests in `tests/Govern.ClientTests/Components/Game`); feature panels in `Features/Games/Components`.

| Component | Status | Use |
|---|---|---|
| `GameSprite`, `GameIcon` | built | The icon set and one icon |
| `StatTable` | built | Icon, label and value rows (`Models/StatRow`), with an optional detail tooltip; `UnitPanel` shows strength (and ranged strength) for any unit, and movement left, sight, cost and build production for your own |
| `NumberTooltip` | built | A number with its exact value and origin (hover, focus, tap) |
| `ProgressBar` | built | Progress towards a cost with "n turns left" at the current rate ("Complete" once reached, "Never at this rate" without income); `CityPanel` shows production (a unit or a building), `UnitPanel` a unit's job (its build production plus the production of the own city whose territory it stands on) |
| `CostLabel` | built | Icon and amount; each Build button shows its production cost, the top bar each resource summed over your cities, and `CityPanel` the city's stock with its income a turn (`+n`) |
| `ConfirmButton` | built | Inline confirm for destructive actions (Resign) |
| `HealthBar` | built (G9) | A unit's health as a bar with "n of max", the danger style at a third or less; `UnitPanel` and the combat forecast. The board draws its own small SVG bar under each hurt unit (`BoardPieces`) |
| `CombatForecastPanel`, `CombatSideView` | built (G9), feature panel | The forecast of a pending attack from `GameEngine.CombatForecast`: each side's health, strength (modifiers in the tooltip) and damage range, then Attack and Cancel |
| `CityPanel` tabs | built, feature panel | For your own city, the stock and the current production (text, cost line, `ProgressBar`) stay on top; the rest is a `RadzenTabs` (`.city-tabs`): **Units** (a Build button per ruleset unit), **Buildings** (the buildings it has, "Built", then a Build button per missing building) and **Economy** (`CityUpkeep` and the next territory tile). Economy carries a `warning` icon while the city can't pay its upkeep. The open tab lives in `GameUiService.CityTab`, so it stays open from city to city; tabs render on the client (`TabRenderMode.Client`), so switching sends nothing. An enemy city has no tabs |
| `CityPanel` buildings | built (G10a), feature panel | The city's buildings, and a Build button per building it doesn't have (with its yield, its cost and the rule's reason when disabled) |
| `CityPanel` stock and territory | built (G5, G7), feature panel | The city's stock (`.city-stock`, one `CostLabel` per resource with its net gain a turn, `+n` or `-n`: income minus the upkeep it pays and the resource cost it pays to begin its item, from `GameEngine.CityForecast`) and "Next territory tile: held of price culture" |
| `CityUpkeep` | built (G8a), feature panel | Inside `CityPanel`: the upkeep a turn (`.upkeep-due`, or "No upkeep"), what the city can't pay for (`.upkeep-shortage`: "Can't pay for Campus, Soldier: each loses 10 health at the end of the turn.") and the order switch, Buildings or Units (`.upkeep-priority-buildings`/`-units`, `aria-pressed`), which sends `SetUpkeepPriorityCommand` |
| `CityPanel` costs and building health | built (G8a), feature panel | `.production-cost`: "Resources paid: X.", "Pays X at the end of the turn to begin.", "Waiting for X more to begin." or, while its production feeds a job, "Pays X to begin once its production is free of the job."; the production bar grows by the forecast's `ProductionPerTurn` (0 while it can't begin or feeds a job); every Build button also shows its resource cost (`ResourceCostLabels`); a hurt building shows "(70 of 100 health)" (`.building-health`) |
| `UnitPanel` upkeep and supply | built (G8a), feature panel | For your own unit: its upkeep (`.unit-upkeep`) and who supplies it (`.unit-supply`, `supplied` or `short`): "Kept up by City 1.", "Fed by the land around it.", or why it loses health at the end of the turn, from `GameEngine.UnitSupply` |
| `YieldList` | planned by G8b | Yield rows per tile and city |
| `QueueList` | planned by G8b/G10b | A reorderable build queue |
| `TreeView` | planned by G11/G12 | Tech, culture and governor skill trees in a full-screen view |
| `GameLog` | built (F11a), feature panel | The player's log in the side panel ([game log](#game-log-in-the-side-panel)) |
| `BoardEffects` | built (F11b), board layer | Damage numbers and pulses of the playing animation ([board animations](#board-animations)) |
| `LensToolbar`, `BoardLens` and `IMapLens` | built (G4) | Toggleable map overlays, one at a time ([map lenses](#map-lenses)); movement cost and yields (G5). Edge marks for river crossings (G6). Later lenses: loyalty (G8), seasons (G14) |

## Map lenses

A lens marks every known tile with one kind of information, instead of cluttering the base map.
- **A lens** is a sealed `Features/Games/Lenses/XxxLens` implementing `IMapLens` (id, name, toolbar icon, and `MarkFor(TileView)` → a `Models/LensMark` with a label, a tint class and a sentence), registered in `DependencyInjection` as `IMapLens`. `NamingConventionTests.ClientMapLenses_AreSealedLensesImplementingIMapLens` and `FolderStructureTests` (`*Lens.cs` only) keep the folder to that.
- **State:** `GameUiService.ActiveLens`, changed by `ToggleLens` (one on at a time; toggling the active one turns it off). Escape clears the selection, not the lens.
- **Toolbar:** `Components/Board/LensToolbar` in the map zone's top-left corner, one toggle button per registered lens (`aria-pressed`, keyboard reachable). It reads the cascaded `GameUiService`, so it renders nothing outside a game screen.
- **Edges:** a lens may also mark the edges a tile stores (`IMapLens.EdgeMarksFor(TileView)` → `Models/EdgeMark`, none by default), drawn after the tiles as a small badge at the edge's middle (`.lens-edge-mark.<class>`).
- **Drawing:** `Components/Board/BoardLens` draws the active lens in the `BoardLayer.Lens` slot: per marked tile a tinted hex (`.lens-mark.<class>` in `board.css`, colours from `--gv-lens-*` tokens), a label and a `<title>` with the sentence, then the edge badges. Unexplored tiles are never marked.
- **Lens marks get the view** (`IMapLens.MarkFor(PlayerView, index)`, defaulting to `MarkFor(TileView)`), so a lens can read the rivers around a tile.
- **Yields** (`YieldLens`, G5): each explored or visible tile shows its total yield (`YieldCalculator.TileYields`: terrain, the best river on its edges, its improvement), tinted `rich` (4 or more), `poor` or `barren` (nothing, like desert), with a sentence naming each resource ("Plains: 2 food, 1 water a turn").
- **Movement cost** (`MovementCostLens`): each explored or visible tile shows its terrain's movement cost (tint `cheap` for 1, `costly` above), and ✕ (`blocked`) where land units can't go. River edges show their extra crossing cost (`+1`, `costly`) or ✕ (`blocked`) where land units can't cross; a free stream shows nothing.

## Rivers

- **Layer:** `Components/Board/BoardRivers` draws every known river edge in the `BoardLayer.Rivers` slot as a `<line class="river river-<id> visible|explored">` between the edge's two corners (`HexLayout.EdgeEnds`), with a `<title>` sentence (`Helpers/RiverDescription`). It renders nothing when no river is known, and again only for a new view, like the terrain.
- **Style:** `board.css` has one `.river-<id>` rule per ruleset river (its width: stream 2.5, river 5, deep river 8, so the kinds differ by more than colour); colours come from the `--gv-river*` tokens, darker for the deep river and when explored. `GameUiTests.BoardCss_StylesEveryRiver` checks a rule exists for every ruleset river. The rules aren't scoped to `.board`, so the `/design` gallery shows each kind.
- **Forecast:** the river bonus appears in the defender's strength breakdown ("+3 behind River").
- **Bridges** (G10a): `BoardRivers` also draws each bridge in sight as a `<line class="bridge">` plank across its edge (`HexLayout.BridgeEnds`, from one tile's centre towards the other), with a `<title>`. The movement cost lens leaves bridged edges unmarked.

## Territory borders

`Components/Board/BoardBorders` (G7) draws, in the `BoardLayer.Borders` slot, every edge of a visible territory tile whose neighbour is not in the same player's territory, as a dashed `<line class="border seat-N">` in the owner's seat colour, set slightly inside the tile so two players' borders on one edge stay apart. It renders nothing while no territory is in sight.

## Improvements

`Components/Board/BoardImprovements` draws each improvement in sight (the view carries it for visible tiles only) in the `BoardLayer.Improvements` slot: its sprite symbol at 0.6 scale in the tile's upper left (`.improvement`, `--gv-improvement`), so the unit or city in the middle stays readable, with a `<title>` naming it.

## Design gallery (/design)

`Features/Design/Pages/DesignPage` at `/design` shows every catalog component with sample data, the icon set (from the ruleset) and the seat colours. It renders only in the Development environment (`IWebAssemblyHostEnvironment.IsDevelopment()`); elsewhere it shows the not-found text, and it has no navigation link. Look there before building a new component, and add every new component to it.

## Performance

The terrain renders once per view (`BoardTerrain.ShouldRender`) and pieces only when the view, selection, highlights or animation motion change (an animation re-renders the pieces once per step); panning and zooming change one `transform`. Maps are 20×14 by default and at most 40×40 (`map.maxSize` in the ruleset), which renders smoothly; measure the largest offered size again before moving terrain to a cached layer or a canvas.

## Adding UI for a new mechanic

1. A new ruleset unit or terrain: add its `<symbol>` to `GameSprite` (and, for a terrain, its two tokens in `tokens.css` and its `.terrain-<id>` rule in `board.css`). The tests name what is missing.
2. A new number or list on a panel: use `StatTable`, `ProgressBar`, `CostLabel`; add a planned component (`YieldList`, `QueueList`, `TreeView`) to `Components/Game` when its G item needs it, with tests and a gallery entry.
3. A new action: a button in `ActionBar` (unit) or in the fitting `CityPanel` tab (city), disabled with the rule's reason; a destructive one uses `ConfirmButton`. A new group of city content gets its own tab rather than lengthening another one.
4. A new selection kind: its panel in `SidePanel`, reading `GameUiService`.
5. New map content: its `BoardLayer` slot. Information a player toggles on and off: a new `IMapLens` ([map lenses](#map-lenses)).
6. Update this document's catalog.
