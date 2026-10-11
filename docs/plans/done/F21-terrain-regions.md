---
title: "Plan: F21 terrain regions on the 3D board (continuous mountain ranges, downs, dune seas, woods and shores)"
tags: [plan, client, board3d, terrain, landscape, mountains, hills, desert, forest, lakes, terrain-regions]
code: [src/Govern.Client/wwwroot/js/board3d, src/Govern.Client/wwwroot/css/tokens.css]
updated: 2026-10-11
---

# Plan: F21 terrain regions on the 3D board

Status: done 2026-10-11 (PR linked in the backlog's "Recently done"). The owner picked "all terrains" from the suggestions (2026-10-11); plan, implementation and close-out go in one PR.

## Context
Each mountain tile was a flat grey shelf with one to three copies of one massif near its middle, so a range read as a grid of cones with no transition between neighbours. Hills, dunes and forests were placed the same way, tile by tile. Andrii asked for natural transitions between mountain tiles and for the other terrains. The fix describes each terrain as a field over the whole map that tiles only sample, so shapes run on across tiles.

## Scope
- In: mountain ranges, downs (hills), dune seas (desert) and woods (forest) as map-wide regions; foothills where hills touch mountains; gorges where a river edge crosses a region; tiles meeting at the mean of their plateaus; snow by altitude; lake beaches and shallows; overlays, pieces, features and walking units following the new ground.
- Out: any rule change (presentation only); new terrains or ruleset changes; real image textures; smooth shading.

## Affected areas
- Client 3D: `wwwroot/js/board3d/ground.js` (regions, strengths, relief, gorges, shared rings), `terrain.js` (ring mesh, colours, crags, `plantWoods`, decorators), `overlays.js` (tints and ring on the rings), `scene.js` (lift pieces, motion and labels; layer keys include river courses), `models.js` (features lifted), `motion.js` (steps climb over ridges).
- Tokens: `wwwroot/css/tokens.css` (`--gv-board3d-beach`, light and dark).
- Reused: the continuous landscape (`ground.js`, PR #62), the massif (`mountains.js`, PR #63), the value noise and patch colours in `terrain.js`.

## Steps
1. `ground.js`: `weightsAt` (a point as plateau, rim corner and rim edge middle shares, as the slope triangles cut it) shared by heights, region strengths and colours. Rims at the mean of the plateaus; region tiles without height jitter.
2. `ground.js`: regions (range, downs, dunes) with a weight per tile, a depth scale (1, 2, 3+ tiles in) and a relief shape in world space (ridged noise, soft swells, crests across a fixed wind). Strength: own on the plateau, mean of the tiles at the rim; fields for the region's tiles and their explored neighbours. A level shelf round the middle at the middle's relief; gorges along river edges.
3. `terrain.js`: every tile's rim has six points per edge, cached by place; region tiles are cut into six rings, others keep the faceted plateau, stitched to the rim. Colours blended like heights; beach on slopes down to a lake; snow over a ragged snowline.
4. `terrain.js`: crags on a range tile's highest point round its shelf; hills and dunes lose their humps; `plantWoods` plants trees on a jittered grid over the map by the forest's blended share; water lighter in the shallows.
5. `overlays.js`, `scene.js`, `models.js`, `motion.js`: tints and ring on the rings; pieces, motion places, lens labels and features lifted by `reliefAt`; walking units climb over the ground.
6. `tokens.css`: `--gv-board3d-beach`.

## Tests
- Unit: none (no Utils, Core or Domain change).
- Integration: none (no server change).
- Client (bUnit): the existing board tests run against the fake renderer; no C# change.
- Architecture: existing `GameUiTests` cover the new token (exists, has a dark value) and the scripts' colour rule.
- Manual: the Basin preview (`/design/maps`) and the bench (`/design/board`), light and dark, with the movement cost lens.

## Docs & rules
- `docs/features/game-ui.md`: `ground.js` and `terrain.js` rows, a "Terrain regions on the board" section, colours, performance.
- `docs/plans/backlog.md`: F21 in Recently done; `docs/README.md` index line.
- No rule change.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` (all green), the bench's JavaScript time and triangles against stage (recorded in the PR).

## Risks & open questions
- Building the terrain layer takes about 1.7 times as long as before (fine grid on region tiles, relief, woods); a move that reveals tiles rebuilds it. Measured on the bench; to revisit if a real phone feels it.
- A unit on a high shelf stands higher than its neighbours; the shelf keeps it level.
