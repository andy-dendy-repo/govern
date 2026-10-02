---
title: "Plan: F18, board hover card and WebGL context loss"
tags: [plan, client, game-ui, board, webgl, hover, tooltip, accessibility, context-loss]
code: [src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Models/TileCard.cs, src/Govern.Client/Features/Games/Models/BoardEvents.cs, src/Govern.Client/Features/Games/Helpers, src/Govern.Client/Features/Games/Services/Board3DInteropService.cs, src/Govern.Client/wwwroot/js/board3d, src/Govern.Client/wwwroot/css/board.css]
updated: 2026-10-02
---

# Plan: F18, board hover card and WebGL context loss

**Status:** done 2026-10-02 (backlog F18), planned and built in one PR at the owner's request, as F17 was; the decisions made while building it are at the end. Client only: Govern.Core, the server, the rules and the view stay as they are.

## Context
The SVG board carried a `<title>` on every lens mark, edge badge, river, bridge, improvement and building, so a player could find out what was on a tile and why a lens showed a number. F17 retired that board, and the WebGL board shows only short labels: `LensMark.Description` and `EdgeMark.Description` are built and then dropped, the canvas shows `cursor: grab` everywhere, and screen readers get no board text (gaps D1–D3 of `/mnt/project-files/reviews/svg-to-3d-regression-review.md`). Separately, a lost WebGL context (common on phones when the tab is backgrounded or the GPU resets) leaves a frozen board with no message (D4). F18 brings the tile text back as one small card, adds a pointer cursor over tiles that do something, gives screen readers the same text, and recovers from or reports a lost context.

## Scope
- In:
  - **Tile card**: a small card for the tile under the mouse pointer (or, on touch, the tile last tapped): terrain name, the tile's rivers and bridges (`RiverDescription`), the improvement or building with "(being built)" for a planned one, the unit and city on it, and the active lens's sentence for that tile and its edges (`LensMark.Description`, `EdgeMark.Description`). Only what the player's view shows (explored, fog rules unchanged).
  - **Pointer cursor** over tiles a click acts on (selectable own piece or city, a highlighted move, attack, build or bridge target); `grab` elsewhere and `grabbing` while dragging, as today.
  - **Screen readers**: a visually hidden polite live region that reads the card of the tile just clicked.
  - **Context loss**: on `webglcontextlost` the render loop stops and the board shows "The board's graphics were reset. Restoring…"; on `webglcontextrestored` the board redraws from the last scene and overlay and the message goes; if it isn't restored within 5 seconds the message offers a Reload button.
- Out: keyboard tile navigation (neither board had it); a hover ring on the hovered tile (the cursor and the card are enough; easy to add later); a card that follows the pointer (fixed corner, decided below); tap-and-hold on touch; moving `BoardMotion` and `HexLayout` to board units (review A1, A2, a separate clean-up item); anything the regression review now running in another thread finds beyond D1–D4, unless the owner adds it.

## Affected areas
- `Client/Features/Games/Models/`: new `TileCard.cs` (title plus lines, each a short sentence); `BoardOverlay.cs` gains `Actionable` (the tiles a click acts on, for the cursor).
- `Client/Features/Games/Helpers/`: new `TileCardBuilder.cs` (static `For(PlayerView view, HexCoord tile, IMapLens? lens, BoardSelection selection)`); `BoardOverlayBuilder.cs` fills `Actionable`. Reuses `RiverDescription`, `Ruleset.TerrainName/ImprovementName/BuildingName`, the lenses' `LensMark`/`EdgeMark` descriptions and `BoardClickResolver`'s idea of what a click on a tile does.
- `Client/Features/Games/Components/Board/GameBoard.razor(.cs)`: renders the card (`aria-hidden`, pointer and touch users) in the board frame's bottom-left corner, the visually hidden live region, and the context-lost message; handles the new relays.
- `Client/Features/Games/Services/Board3DInteropService.cs`: the relay gains `OnTileHovered(int q, int r)`, `OnHoverEnded()`, `OnContextLost()`, `OnContextRestored()`; `MountAsync` takes the extra callbacks.
- `Client/wwwroot/js/board3d/scene.js`, `gestures.js`: on pointer move (mouse and pen only, at most once per animation frame, and only while not dragging) raycast the same way `click` does and relay only when the tile changes; leave relays hover ended; set the canvas cursor from `overlay.actionable`; listen for `webglcontextlost` (prevent default so the browser can restore) and `webglcontextrestored`, pause and resume the loop.
- `Client/wwwroot/css/board.css`: `.board-tile-card` (tokens only, light and dark), `.board3d-lost`; a shared visually hidden class if none exists.

## Steps
1. Client models and helpers: `TileCard`, `TileCardBuilder`, `BoardOverlay.Actionable` filled by `BoardOverlayBuilder`. Builds on its own; nothing reads them yet.
2. Interop: the new relay methods on `Board3DInteropService` and the hover raycast, cursor and context-loss handlers in `js/board3d`.
3. `GameBoard`: hovered tile → card; clicked tile → live region text (and the card on touch); hover ended or a drag starts → card hidden; the card is rebuilt when the view, lens or selection changes under a still pointer.
4. `GameBoard`: the context-lost message, the redraw on restore (`DrawAsync` + `OverlayAsync` with the change keys reset), the 5-second Reload offer.
5. CSS and the doc comments that still say "the SVG board read it out" (`LensMark`, `EdgeMark`).
6. Build after each step (`dotnet build Govern.slnx`).

## Tests
- Unit: none (no Utils, Core or Domain change).
- Integration: none (no server change).
- Client (bUnit):
  - `TileCardBuilderTests`: terrain only; a river edge and a bridge; an improvement; a planned building reads "(being built)"; own and enemy unit and city; the movement-cost and yields lens sentences, including an edge badge; an unexplored tile gives no card; a fogged tile shows only what the view keeps.
  - `BoardOverlayBuilderTests`: `Actionable` holds own pieces and cities and the highlighted targets, not empty or enemy-only tiles when nothing is selected.
  - `GameBoardTests` via `Support/FakeBoardRenderer` (gains hover and context relays): hovering shows the card and hover ended hides it; the card follows a lens switch; a click fills the live region; context lost shows the message and restored redraws (a new `render` and `renderOverlay` call) and hides it; Reload appears after the timeout.
  - `Board3DInteropServiceTests`: the relay forwards the new callbacks.
- Architecture: none new; `Board3DScripts_ReadOnlyTokensThatExist` and the token checks cover the new CSS and script.

## Docs & rules
- `docs/features/game-ui.md`: the game board section gains the tile card, the cursor, the live region and context loss; the interaction rules mention hover and tap.
- `docs/features/client.md`: the interop relay list.
- `docs/README.md`: index line for this plan (moves with it to done).
- `.claude/rules/game-ui.md`: what the tile card shows comes from C# (`TileCardBuilder`), as with the scene and overlay; a new board feature with text adds its line there.
- Backlog close-out in the same PR: plan to `docs/plans/done/`, F18 to Recently done, links fixed.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green.
- Run the client with Playwright at 1400 and 390 wide, light and dark: hover a farm, a river edge with a bridge, a building being built and a tile under each lens and read the card; the cursor turns to a pointer over an own unit and its move targets; tap on the phone width shows the card. Trigger `WEBGL_lose_context.loseContext()` from the console: the message shows, `restoreContext()` brings the board back as it was. Screenshots to `/mnt/project-files/screenshots/f18-hover-card/`.

## Risks & open questions
- **Card position:** a fixed bottom-left card never hides the tile the player is looking at or the anchored attack forecast, and works the same on touch; a card at the pointer would cover the next tile and need a JS call per move. Reversible if it feels far from the tile.
- **Restore after context loss:** Three.js re-creates its GL state on restore and re-uploads geometry and textures on the next frame; if a layer (the baked unit models or the label canvas) does not come back, the restore path rebuilds the whole scene from the last `BoardScene` instead. Checked in the browser with `WEBGL_lose_context`.
- **Hover cost:** one raycast per animation frame while the mouse moves, relayed only on a tile change; the `/design/board` bench at 40×40 shows whether it costs frame time.

## Decisions made while building
- **Card corner:** the selection card already sits bottom left, so the tile card went to the bottom-right corner; on phones (where the selection card is a bottom sheet) it sits under the zoom buttons. The log drawer covers it while open.
- **Cursor from .NET, not the overlay:** instead of an `Actionable` list in `BoardOverlay`, `GameBoard` answers each hovered tile with `Board3DInteropService.SetPointerAsync` (and again when the selection, highlights, view or busy state change), so the overlay and its tests stay as they were and a busy board never shows the pointer. `BoardOverlayBuilder` is unchanged.
- **Touch:** a click or tap sets the card's tile too, so a phone shows the tapped tile's card; any drag (or the mouse leaving the map) puts it away.
- **Restore:** `scene.js` keeps the last scene and overlay, so on `webglcontextrestored` it builds every layer again itself and tells .NET only to hide the message; `GameBoard` sends nothing again. Checked in Chromium with `WEBGL_lose_context`: the message shows, Reload appears after 5 seconds, and `restoreContext()` brings the board back as it was.
- **Relay:** `Board3DInteropService.MountAsync` takes a `Models/BoardEvents` (click, hover and context callbacks; the last two optional) instead of the click callback alone.
- **Card text:** each line is "Kind: value" ("Your unit: Soldier, health 40 of 100", "Building: Campus (being built)", "Territory of Kyiv"), rivers and lens sentences as `RiverDescription` and the lenses write them. A river and a lens edge sentence show on both tiles of the edge.
- **Browser check:** done on the `/design/board` bench at 1400 wide in light and dark (screenshots in `/mnt/project-files/screenshots/f18-hover-card/`); the game screen's phone layout needs the Api running and was not checked in the browser.
