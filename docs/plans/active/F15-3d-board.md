---
title: "Plan: F15 3D game board (WebGL, Three.js)"
tags: [plan, client, board, 3d, webgl, threejs, rendering, visuals, game-ui, animations, lenses]
code: [src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Services, src/Govern.Client/wwwroot/js, src/Govern.Client/wwwroot/css/tokens.css]
updated: 2026-10-02
---

# Plan: F15 3D game board

**Status:** in progress since 2026-10-02 (backlog F15): step 1 (foundation and terrain) merged in PR #41; step 2 (rivers, bridges, improvements, buildings, borders) done in PR #42 ([its plan](../done/F15-step2-rivers-improvements.md)); step 3 planned in [its own plan](F15-step3-pieces-selection-lenses.md). Visuals only: Govern.Core, the server, the rules and the view stay as they are.

## Context
Andrii compared Govern's flat SVG hex board with a Three.js prototype by another developer (Yarik0378/game, a gridless city builder) and finds that look much better. Its look comes from a 3D WebGL scene: ground with height, flat-shaded low-poly models built in code from simple shapes, see-through water, sun and soft shadows, a tilted camera, and pastel colours. This plan replaces Govern's SVG board with a Three.js board in that art direction, drawn on Govern's own hexes, rivers on edges and units on tiles. Their code is not copied (the repo has no licence, and its map model is different); only the approach is reused. Three.js is MIT.

## Scope
- In: a WebGL board that draws everything the SVG board draws today (terrain, rivers and bridges, improvements and buildings, territory borders, lenses, highlights, cities, units, health bars, animations and effects), the same click and selection behaviour, pan, zoom and a tilted camera, fog for explored and unexplored tiles, light and dark themes from the existing tokens, reduced motion, and removing the SVG board once the 3D one matches it.
- Out: game rules and the view (no change); the side panels and action bar (a "paper card" restyle of the panels is a separate item); camera rotation; seasons or day/night (G14); sound; imported 3D model files (every model is built in code, so there are no asset licences); a canvas fallback when WebGL is missing (the board shows a message instead, see risks).

## Affected areas
- `Client/Features/Games/Models/BoardScene.cs` (new): the plain data the renderer draws, one record per thing (`SceneTile`, `SceneEdge`, `ScenePiece`, `SceneMark`…), built in C# so the rules for what shows stay in .NET and are unit-testable with bUnit.
- `Client/Features/Games/Helpers/BoardSceneBuilder.cs` (new): `PlayerView` + `BoardSelection` + `BoardHighlights` + active lens + `BoardMotion` → `BoardScene`. Reuses `HexLayout`, `RiverDescription`, the `IMapLens` marks and `Seats`.
- `Client/Features/Games/Helpers/TerrainReliefs.cs` (new): per ruleset terrain the 3D shape (tile height, and which decoration: trees, humps, peaks, water). Presentation only, not a game number, so it stays out of the ruleset.
- `Client/Features/Games/Components/Board/GameBoard.razor(.cs)`: hosts a `<canvas>` instead of the `<svg>`, keeps `LensToolbar` and the zoom buttons, sends the scene to JS when it changes, and receives tile clicks.
- `Client/Features/Games/Services/BoardInteropService.cs`: grows `MountAsync`, `RenderAsync(BoardScene)`, `FocusAsync`, `FitAsync`, `DisposeAsync`, and a `[JSInvokable]` tile-click callback; keeps the no-op fallback.
- `wwwroot/js/board3d/` (new): `board.js` (entry), `scene.js` (renderer, lights, resize), `camera.js` (pan, zoom, tilt), `terrain.js` (hex tiles, water, fog), `models.js` (units, cities, improvements, buildings, bridges from primitives), `overlays.js` (highlights, lens tints, borders), `labels.js` (city names, lens labels and damage numbers as HTML over the canvas), `motion.js` (tweens for `BoardMotion`).
- `wwwroot/lib/three/` (new): the vendored Three.js ES module build and its `LICENSE`, served from our own origin.
- `wwwroot/css/tokens.css`: board tokens gain the 3D palette (sky, water, light, terrain tops and sides); `board.css` keeps the canvas frame and HTML labels.
- Removed in step 6: `BoardTerrain`, `BoardRivers`, `BoardImprovements`, `BoardBorders`, `BoardLens`, `BoardPieces`, `BoardEffects`, `BoardViewport`, `BoardClickResolver` and their tests, `wwwroot/js/board.js`.

## Steps
Each step is one PR to `stage`. Until step 6 the 3D board sits behind a "3D board (preview)" toggle (`GameUiService.Board3D`, off by default), so every PR ships a working game.

1. **Foundation and terrain.** Vendor Three.js into `wwwroot/lib/three` (the current release at implementation time, with its licence). `BoardScene`, `BoardSceneBuilder` (tiles only), `TerrainReliefs`. Until step 3 a 3D click only selects (nothing acts on pieces the board doesn't draw yet). JS: renderer, hemisphere and sun light with soft shadows, hex tiles as flat-shaded prisms raised by terrain, forest, hills and mountain decorations, lake water as translucent turquoise over a seabed, explored tiles faded toward parchment, unexplored tiles under low clouds. Camera: drag to pan, wheel and buttons to zoom, tilt that steepens as it zooms out, Fit, and opening on the player's own pieces. A click raycasts to a tile and calls back with its `HexCoord`; a drag is never a click.
2. **Rivers and improvements** ([detailed plan](../done/F15-step2-rivers-improvements.md), done). Rivers as sunken water ribbons along hex edges (width by kind, as today), bridges as plank models, improvements and buildings as small models in the tile (planned buildings faded), territory borders as raised dashed lines in seat colour.
3. **Pieces, selection and lenses** ([detailed plan](F15-step3-pieces-selection-lenses.md)). Units and cities as models in seat colours with the existing sprite symbol on a small banner so they read at any zoom, city names as HTML labels, health bars, selection ring, reachable, attackable, target, bridgeable and placeable highlights as tinted tile tops, lens tints and labels and edge badges. Clicks then act as on the 2D board; picking also tests the pieces and tall decorations.
4. **Animations.** `BoardMotion` drives JS tweens: units walk tile by tile, attacks lunge, damage numbers float, destroyed units fade, tiles pulse; a click or a newer view finishes them; reduced motion skips moves and lunges.
5. **Themes, touch and performance.** Pinch to zoom with two fingers (today only the buttons zoom on a phone). Read every colour from the `--gv-*` tokens at mount and on theme change, so light and dark come from `tokens.css` alone. Instance repeated meshes, rebuild only changed layers, and check a 40×40 map stays smooth on a mid-range laptop and a phone.
6. **Switch over.** The 3D board becomes the only board: remove the toggle, the SVG board components, their tests and `board.js`; update the docs.

## Tests
- Unit: none (client only; Core does not change).
- Integration: none (no server change).
- Client (bUnit): `BoardSceneBuilder` per layer (tile height and fog state per visibility, rivers and bridges per edge, pieces with seat and health, highlights, lens marks, ghosts from `BoardMotion`); `GameBoard` sends a new scene only when the view, selection, highlights, lens or motion change, turns a JS tile click into `TileClicked`, and shows the no-WebGL message when mount fails; `BoardInteropService` degrades to no-ops; `TerrainReliefs` has an entry for every ruleset terrain.
- Architecture: `GameUiTests` extends the "colour values only in tokens" rule to `wwwroot/js/board3d`, and the client test `TerrainReliefsTests.Known_EveryRulesetTerrain_HasARelief` checks every ruleset terrain has a relief. `ClientSecurityTests` scans every `wwwroot` script for HTML sinks, eval and `localStorage`: our `board3d` modules must pass as they are; if the vendored Three.js build trips a check, `wwwroot/lib` is excluded by name with the reason in the test, and a test pins that only the listed vendor files sit there. The SVG-only checks (`BoardCss_MapsEveryTerrainToItsPalette`, `BoardCss_StylesEveryRiver`, the board layer order test) move to their 3D equivalents in step 6.
- Manual: Playwright screenshots of the board, a selected unit, an attack forecast, each lens, light and dark, at 1400 and 390 px wide, saved to `/mnt/project-files/screenshots/f15-3d-board/` in each PR.

## Docs & rules
- `docs/features/game-ui.md`: the board sections (layers, rivers, borders, improvements, lenses, animations, performance) describe the 3D scene; a new "Board renderer" section on `BoardScene`, the JS modules and adding a model for a new unit, terrain or building.
- `docs/features/client.md`: vendored JS libraries and where they live.
- `docs/README.md` index, and this plan moved to `done/` with the backlog close-out in the step 6 PR.
- `.claude/rules/blazor-client.md` and `game-ui.md`: the board is drawn in `wwwroot/js/board3d` from a `BoardScene`; what shows is decided in C#, the JS only draws and reports clicks; colours only from tokens; models built in code, no imported asset files.

## Verification
- `dotnet build Govern.slnx`, `dotnet test Govern.slnx` and `dotnet format Govern.slnx --verify-no-changes` green on every PR.
- `dotnet run --project src/Govern.Api` and `src/Govern.Client`, play a game against a second account, and check with Playwright: moves, attacks with the forecast, bridges, placing a building, both lenses, the log's Show button centring the board, Esc, reduced motion, light and dark.
- The CSP in `staticwebapp.config.json` stays unchanged (all scripts are same-origin).

## Risks & open questions
- **Board tests get thinner.** bUnit can't see a canvas, so it tests the scene data, not the drawing. Visual checks are manual screenshots. A Playwright test project could cover this later; it is not part of this plan.
- **WebGL missing or disabled** (very old devices, some locked-down browsers): the board shows a message instead of a map. Keeping the SVG board as a fallback would double the board's upkeep, so it is removed in step 6.
- **Phones:** shadows and antialiasing cost GPU time; step 5 lowers them on small screens if a 40×40 map stutters.
- **Vendored code and the security tests:** `ClientSecurityTests` was written for our own scripts. Step 1 runs it against the Three.js build first; an exclusion for `wwwroot/lib` needs the `security-auditor` agent's review in that PR.
- **Accessibility:** tiles are no longer DOM elements. Every action stays a button, as today, and the canvas keeps `role="application"` with its label; nothing that was keyboard-reachable before becomes unreachable.
