---
title: "Plan: F17, retire the SVG board"
tags: [plan, client, game-ui, board, 3d, svg, webgl, cleanup]
code: [src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Components/GameTable.razor, src/Govern.Client/Features/Games/Services/GameUiService.cs, src/Govern.Client/wwwroot/css/board.css, src/Govern.Client/wwwroot/js]
updated: 2026-10-02
---

# Plan: F17, retire the SVG board

**Status:** done 2026-10-02 in PR #50 (backlog F17), planned and built in one PR at the owner's request; the decisions made while building it are at the end. Visuals and client code only: Govern.Core, the server, the rules, the view and the 3D renderer's drawing stay as they are.

## Context
F15 built a WebGL (Three.js) board next to the SVG board, behind a 3D button, and F16 put both under the full-screen game view. The 3D board now draws everything the SVG board draws, plays the same animations and handles touch, so keeping two boards doubles every board change and keeps a toggle players don't need. F17 makes the 3D board the only board and deletes the SVG board, its toggle, its JavaScript and the tests and rules that exist only for it, leaving the client with one board path.

## Scope
- In: the game screen and the `/design/board` bench show only the 3D board; remove the 2D/3D toggle (`BoardModeToggle`, `GameUiService.Board3D`/`ToggleBoard3D`); delete the SVG board components, `BoardLayers`, `BoardViewport`, `BoardAnchor`, `BoardInteropService` and `wwwroot/js/board.js`; move the reduced-motion read to the game screen's script; drop the SVG-only CSS from `board.css`; rename `GameBoard3D` to `GameBoard`; port the game table tests from SVG tiles to the renderer's click relay; update docs and rules.
- Out: a fallback board when WebGL is missing (the board says it can't show and the rest of the screen still works; decided below); renaming the renderer's own parts (`Board3DInteropService`, `js/board3d`, the `board3d-*` classes and `--gv-board3d-*` tokens name the WebGL implementation, and renaming them is churn with no gain); reworking `BoardAnimationService`'s `BoardMotion` (it stays in `HexLayout` points and `SceneMotionBuilder` converts it, as today); the panel icons (`GameSprite`, `GameIcon`) and the `/design` gallery's terrain and river swatches, which keep the CSS they use; findings of the parallel SVG→3D regression review, which land as their own items.

## Affected areas
- `Client/Features/Games/Components/Board/`: delete `GameBoard.razor(.cs)` (SVG), `BoardTerrain`, `BoardRivers(.cs)`, `BoardImprovements(.cs)`, `BoardBorders(.cs)`, `BoardLens`, `BoardPieces`, `BoardEffects`, `BoardModeToggle`; rename `GameBoard3D.razor(.cs)` to `GameBoard.razor(.cs)`, which takes over `ZoomStep`. `LensToolbar` stays.
- `Client/Features/Games/Helpers/`: delete `BoardLayers.cs` (with `Enums/BoardLayer` if nothing else uses it), `BoardViewport.cs`, `BoardAnchor.cs`. `HexLayout`, `BoardClickResolver` and the scene builders stay.
- `Client/Features/Games/Services/`: delete `BoardInteropService.cs`; `GameScreenInteropService` gains `PrefersReducedMotionAsync`; `GameUiService` loses `Board3D` and `ToggleBoard3D`.
- `Client/Features/Games/Components/GameTable.razor(.cs)`: one board; reduced motion from `GameScreenInteropService`.
- `Client/Features/Design/Pages/BoardBenchPage.razor(.cs)`: one board, no toggle.
- `Client/DependencyInjection.cs`: no `BoardInteropService`.
- `Client/wwwroot/js/board.js`: delete; `js/game-screen.js` gains `prefersReducedMotion`.
- `Client/wwwroot/css/board.css`: keep the frame, zoom buttons, lens toolbar, the terrain palettes and `.terrain-mark` (panel icons), river and bridge swatch styles (design gallery), seat classes, `.icon-mark`, the reduced-motion rule for the 3D damage label and the `board3d-*` rules; drop the SVG board rules (`.board`, tiles, terrain symbols on tiles, improvements, buildings, borders, lens marks and edge badges, overlays, health track, pieces, city names, unit and ghost transitions, SVG effects and their keyframes except those the 3D labels use).
- Models whose doc comments name the SVG components (`LensMark`, `IMapLens`, `BoardOverlayBuilder`, `ScenePieceBuilder`, `SceneLayerBuilder`): point them at the 3D board.

## Steps
1. Client: rename `GameBoard3D` to `GameBoard` (component, code-behind, `ZoomStep` moves in), delete the SVG components, `BoardModeToggle`, `BoardLayers`/`BoardLayer`, `BoardViewport`, `BoardAnchor`.
2. Client: `GameUiService` without the toggle; `GameTable` and `BoardBenchPage` render the one board.
3. Client: `BoardInteropService` and `board.js` go; `prefersReducedMotion` moves to `game-screen.js` behind `GameScreenInteropService.PrefersReducedMotionAsync`.
4. Client: the WebGL message drops "Switch back with the 2D button"; `board.css` loses the SVG rules; doc comments updated.
5. Build after each step (`dotnet build Govern.slnx`).

## Tests
- Unit: none (no Utils, Core or Domain change).
- Integration: none (no server change).
- Client (bUnit):
  - Delete `GameBoardTests` (SVG), `BoardModeToggleTests`, `BoardViewportTests`, `BoardAnchorTests`, `BoardInteropServiceTests` and `GameUiServiceTests.ToggleBoard3D_…`.
  - Rename `GameBoard3DTests` to `GameBoardTests`.
  - A shared `Support/FakeBoardRenderer` sets up the `board3d` module (mount succeeds, every call recorded) and clicks a tile through the mount's relay, as the renderer does. The `GameTable*Tests` that clicked SVG tiles click through it, and those that read SVG layers (lens marks, highlights, pieces) read the overlay or scene sent to the renderer.
  - `GameScreenInteropService` reads reduced motion and degrades to `false` when the module fails; `GameTable` passes it to the animation service.
  - `BoardBenchPageTests` and `DesignPageTests` lose their SVG checks.
- Architecture: `GameUiTests` keeps `BoardCss_MapsEveryTerrainToItsPalette` and `BoardCss_StylesEveryRiver`: the panel terrain icons and the gallery's river swatches still use those rules (messages updated). The 3D equivalents already exist and stay: `Tokens_DefineAPaletteForEveryTerrain` with `Board3DScripts_ReadOnlyTokensThatExist` for the colours, `TerrainReliefsTests`/`RiverReliefsTests` for the shapes and widths. The SVG layer order test goes with the SVG board. No new rule: the existing ones cover the one board.

## Docs & rules
- `docs/features/game-ui.md`: "3D board (preview, F15)" becomes "Game board"; "Board layers" and the 2D parts of "Board animations" go; reduced motion through `GameScreenInteropService`.
- `docs/features/client.md`: the board section describes the WebGL board; `board.js` and the SVG rows go; tests list updated.
- `docs/README.md`: index lines for game-ui, client and this plan; `docs/game/design-roadmap.md` visual-language and performance rows no longer name the SVG layer order.
- `.claude/rules/blazor-client.md` (board components and JS), `game-ui.md` (no `GameBoardTests` layer order, `<use>` on the board), `client-tests.md` (board tests through the renderer relay).
- Backlog: F17 leaves Deferred for Recently done, this plan moves to `docs/plans/done/` in the same PR.

## Verification
- `dotnet build Govern.slnx` (warnings are errors), `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green.
- `grep -rn "Board3D\b\|BoardModeToggle\|BoardInteropService\|js/board.js" src tests docs .claude` finds nothing outside done plans.
- Run the client: the game screen and `/design/board` show the 3D board with no 2D button; clicks select and move; the lens toolbar and zoom buttons work.

## Risks & open questions
- **WebGL missing or off:** with no SVG fallback, such a browser can't show the map (the panels, End turn and Resign still work). WebGL is on in every current desktop and phone browser; a fallback would mean keeping the SVG board, which is what F17 removes. Decision: no fallback, the board says why. Reversible by reviving the SVG board from git if players hit it.
- **Test coverage moves, not lost:** the SVG `GameTable` tests are the main coverage of select → move → attack; they are ported to the renderer relay rather than deleted.

## Decisions made while building
- **Tokens and CSS trimmed with the board:** the tokens only the SVG board read (`--gv-board-background`, `--gv-board-outline`, `--gv-tile-*`, `--gv-overlay-*-stroke`, `--gv-improvement`, `--gv-terrain-mark-explored`) are gone, and the `.terrain-<id>` rules keep only the icon colour. The gallery's river swatches no longer take a `visible` class.
- **Lens sentences:** `LensMark.Description` and `EdgeMark.Description` stay, but nothing reads them now: the SVG board showed them as `<title>`s and the WebGL board has no accessible text per tile yet. Backlog idea F18 (a hover card for the tile) would read them again.
- **Tests:** the game table tests that clicked SVG tiles now click through `Support/FakeBoardRenderer` and read the overlay, scene and motion it was sent; tests whose newer view is meant to be drawn again carry a new view version, as real views do. `GameScreenInteropServiceTests` cover the reduced-motion read, and `GameTableAnimationTests` check that a player who asked for reduced motion sees no walk.
- **Lens toolbar:** without the 2D/3D button the game screen's toolbar uses the board's own `right: 8rem` stop instead of `10rem`.
