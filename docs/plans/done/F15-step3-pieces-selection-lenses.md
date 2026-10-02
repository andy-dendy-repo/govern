---
title: "Plan: F15 step 3, pieces, selection, highlights and lenses on the 3D board"
tags: [plan, client, board, 3d, webgl, threejs, units, cities, selection, highlights, lenses, labels, clicks]
code: [src/Govern.Client/Features/Games/Helpers, src/Govern.Client/Features/Games/Models, src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/wwwroot/js/board3d]
updated: 2026-10-02
---

# Plan: F15 step 3, pieces, selection and lenses on the 3D board

**Status:** done 2026-10-02 in PR #43 (backlog F15, step 3 of the [F15 plan](../active/F15-3d-board.md)). Visuals only: Govern.Core, the server, the rules and the view stay as they are. The 3D board stays behind its preview toggle.

## Context
Steps 1 and 2 (PRs #41, #42) put the terrain, rivers, bridges, improvements, buildings and borders on the 3D board, but no units or cities, so a 3D click may only select. This step draws everything the player acts on: units and cities, the selection, the move, attack, bridge and placing highlights, and the two map lenses. With them on screen, a 3D click acts like a 2D one, and the 3D board becomes playable. It also unblocks F16 (the full-screen game view).

## Scope
- In: units and cities as low-poly models in seat colours, each with its sprite symbol on a small banner so it reads at any zoom; city names and health bars; the selection ring; reachable, attackable, target, bridgeable and placeable tiles as tinted tile tops; the active lens as tile tints with labels plus its edge badges; clicks that act (move, attack, bridge, place) as on the 2D board; picking that also hits pieces.
- Out: animations, so pieces jump to their new tile and `BoardMotion` ghosts and effects stay on the 2D board (step 4); pinch zoom and incremental layer rebuilds (step 5); keyboard tile navigation on the canvas (every action stays a panel button, as on the 2D board).

## Affected areas
- `Client/Features/Games/Models/` (new): `ScenePiece.cs` (tile, position, height, kind `unit` or `city`, model, seat, own, health share, name for a city, sprite icon id) in the `BoardScene`; `BoardOverlay.cs` with `OverlayTile.cs` (tile, position, height, tint: `reachable`, `attackable`, `target`, `bridgeable`, `placeable` or a lens class, optional label and description) and `OverlayEdge.cs` (edge middle, height, label, class, description), plus the selected tile.
- `Client/Features/Games/Enums/PieceModel.cs` (new): `Settler`, `Builder`, `Soldier`, `Spearman`, `Horseman`, `Archer`, `City`. `Helpers/PieceModels.cs` maps ruleset unit ids to a model, with a fallback (`Soldier`) so a new unit still draws; `PieceModelsTests` checks every ruleset unit has one.
- `Client/Features/Games/Helpers/ScenePieceBuilder.cs` (new; planned as a layer of `SceneLayerBuilder`, split out to keep that file small): units and cities from the view, with `Seats`, `Icons` and the ruleset's full health.
- `Client/Features/Games/Helpers/BoardOverlayBuilder.cs` (new): `BoardSelection` + `BoardHighlights` + active `IMapLens` + view → `BoardOverlay`, reusing `IMapLens.MarkFor`/`EdgeMarksFor` and `HexLayout.EdgeMiddle`. Highlights are drawn over a lens, as on the 2D board.
- `Client/Features/Games/Components/Board/GameBoard3D.razor(.cs)`: takes `Selection`, `Highlights`, `Lens` and `MaxHealth` like `GameBoard`, sends the overlay when one of them or the view changes (by reference, so panning and zooming never rebuild it), and gets `LensToolbar` next to the zoom buttons.
- `Client/Features/Games/Components/GameTable.razor(.cs)`: passes the same parameters to `GameBoard3D` and drops the "3D clicks only select" guard.
- `Client/Features/Games/Services/Board3DInteropService.cs`: `RenderOverlayAsync(BoardOverlay)`.
- `wwwroot/js/board3d/pieces.js` (new): unit figures and cities built from simple shapes, coloured by seat, own pieces a touch brighter, and picking by instance.
- `wwwroot/js/board3d/overlays.js`: tinted tile tops (one translucent hex per overlay tile, just above its surface), the selection ring (a raised hex outline in `--gv-selected`), and the target in its stronger tint.
- `wwwroot/js/board3d/labels.js` (new): HTML over the canvas, re-placed after every camera move: a banner per piece (the `GameSprite` symbol via an SVG `use`), city names, health bars (low at a third or less, as `HealthBar`), lens labels and edge badges. Built with `createElement`/`createElementNS` and `textContent` only, so `ClientSecurityTests` still finds no HTML sinks; `aria-hidden`, since the side panel says the same.
- `wwwroot/js/board3d/palette.js`: `tint(name)` returns a colour and its alpha, because the overlay and lens tokens are `rgba()` values.
- `wwwroot/js/board3d/scene.js`: the overlay is its own group, rebuilt alone; clicks raycast the ground and the pieces and report the nearest tile.
- `wwwroot/css/board.css`: `.board3d-labels` (the label layer, `pointer-events: none`), banner, name, health and lens label styles from existing tokens.
- `wwwroot/css/tokens.css`: `--gv-board3d-figure` (faces and hands) and `--gv-board3d-horse`, light and dark.

## Steps
1. Client models and enum: `ScenePiece`, `PieceModel`, `BoardOverlay`, `OverlayTile`, `OverlayEdge`; `BoardScene` gains `Pieces`.
2. Client helpers: `PieceModels`, `ScenePieceBuilder` and `BoardOverlayBuilder`.
3. Components: `GameBoard3D` takes the overlay inputs and sends the overlay; it hosts the `LensToolbar`. `GameTable` passes them and lets 3D clicks act.
4. Services: `Board3DInteropService.RenderOverlayAsync`.
5. JS: `pieces.js`, the overlay in `overlays.js`, `labels.js`, `palette.tint`, and picking pieces in `scene.js`.
6. CSS and tokens: the label layer and the two model tokens.

## Tests
- Unit: none (client only; Core does not change).
- Integration: none (no server change).
- Client (bUnit):
  - `ScenePieceBuilderTests`: units and cities appear with their model, seat, own flag, banner icon, health share and city name.
  - `BoardOverlayBuilderTests`: each highlight kind gives its tint; the target is marked; the selected unit's or city's tile is the selection; a lens gives its marks and edge badges; with no selection and no lens the overlay is empty.
  - `PieceModelsTests`: every ruleset unit has a model.
  - `GameBoard3DTests`: a new selection, highlight or lens sends an overlay and no new scene; the same ones send nothing; the lens toolbar shows.
  - `GameTableTests`: the step 1 test "a 3D click selects but never acts" turns into "a 3D click moves the selected unit".
  - `Board3DInteropServiceTests`: the overlay degrades to a no-op.
- Architecture: `GameUiTests` gets a builder check per `PieceModel` in `pieces.js` (like the `FeatureModel` one); the token and security scans already cover the new JS.
- Manual: Playwright in a real game, saved to `/mnt/project-files/screenshots/f15-3d-board/step3/`: select a unit and move it, open an attack forecast and attack, the movement and yields lenses, a city with its name, light and dark, 1400 and 390 px.

## Docs & rules
- `docs/features/game-ui.md`, "3D board (preview, F15)": pieces, banners and labels, the overlay and how it is sent apart from the scene, picking, and clicks that act.
- `.claude/rules/game-ui.md`: "Every ruleset id has its UI" adds the 3D model (`PieceModels`) for a new unit.
- `.claude/rules/blazor-client.md`: the 3D board's HTML labels are built without HTML sinks (`textContent`, `createElementNS`).
- `docs/README.md` index; the [F15 plan](../active/F15-3d-board.md) step 3 links here; the backlog F15 row and F16 (now unblocked, needs plan).
- At merge (in the step 3 PR): this plan moves to `docs/plans/done/`.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes` and `dotnet test Govern.slnx` green.
- Run the Api and client with two accounts, play several turns on the 3D board only (found a city, move, attack with the forecast, build a bridge, place a building, both lenses, the log's Show button, Esc), and compare it with the 2D board.

## Risks & open questions
- **Overlay and lens colours are `rgba()` tokens.** Three.js colours have no alpha, so `palette.tint` reads the alpha into the material's opacity; the overlay tokens stay as they are for both boards.
- **Labels and performance:** one HTML banner per piece is re-placed on every camera move. On a 40×40 map with many units that may stutter; step 5 can hide banners for pieces off screen or far away.
- **Units jump in this step:** without step 4 a move or a kill on the 3D board is instant. The 2D board keeps its animations, and step 4 follows.
