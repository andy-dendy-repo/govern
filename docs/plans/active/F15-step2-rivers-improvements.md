---
title: "Plan: F15 step 2, rivers, bridges, improvements, buildings and borders on the 3D board"
tags: [plan, client, board, 3d, webgl, threejs, rivers, bridges, improvements, buildings, territory, borders]
code: [src/Govern.Client/Features/Games/Helpers, src/Govern.Client/Features/Games/Models, src/Govern.Client/wwwroot/js/board3d, src/Govern.Client/wwwroot/css/tokens.css]
updated: 2026-10-02
---

# Plan: F15 step 2, rivers, improvements and borders on the 3D board

**Status:** planned 2026-10-02 (backlog F15, step 2 of the [F15 plan](F15-3d-board.md)). Visuals only: Govern.Core, the server, the rules and the view stay as they are. The 3D board stays behind its preview toggle.

## Context
Step 1 (PR #41) put the terrain on the 3D board. The 2D board also shows rivers on tile edges, bridges, improvements, buildings (with the faded one a city is producing) and territory borders, and the 3D board has to show all of them before it can replace the SVG one. This step adds those five things as low-poly models built in code, decided in C# like the tiles, so a player in 3D sees the same map they see in 2D. Pieces, highlights and lenses stay for step 3.

## Scope
- In: rivers as water ribbons along hex edges (wider and darker per kind, faded on explored tiles), bridges as plank decks across their edge, improvements and buildings as small models in the tile's upper left corner (buildings in the owner's seat colour, planned ones faded), territory borders as raised dashed lines in seat colour just inside the owned tile, and the tile decorations (trees, humps) making room for an improvement or building.
- Out: units, cities, selection, highlights, lenses and HTML labels (step 3); animations (step 4); pinch zoom, rebuilding only the changed layers and instancing tuning (step 5); clicking on a river, bridge or building (clicks still pick tiles only); river flow animation.

## Affected areas
- `Client/Features/Games/Models/BoardScene.cs`: gains `Rivers`, `Bridges`, `Features` and `Borders` lists.
- `Client/Features/Games/Models/` (new): `SceneRiver.cs` (two corner points, height, width, tone, visibility), `SceneBridge.cs` (from one tile centre side to the other, both heights), `SceneFeature.cs` (position, height, model, seat or 0, planned, visibility) and `SceneBorder.cs` (two inset points, height, seat).
- `Client/Features/Games/Enums/FeatureModel.cs` (new): the 3D model kinds: `Field`, `Camp`, `Mine`, `Tents`, `Hall`, `Dome`, `Stall`, `Pier`. The JS keys its builders by model kind, never by a ruleset id.
- `Client/Features/Games/Helpers/FeatureModels.cs` (new): ruleset improvement and building id → `FeatureModel` (farm → Field, lumber_camp → Camp, mine → Mine, army_camp → Tents, campus → Dome, trade_post → Stall, culture_centre → Hall, harbour → Pier), with a fallback (`Hall` for buildings, `Field` for improvements) so a new ruleset id still draws.
- `Client/Features/Games/Helpers/RiverReliefs.cs` (new): ruleset river id → width in board units and tone (`river` or `deep`): stream 0.09, river 0.17, deep river 0.27 (the SVG widths 2.5, 5 and 8 divided by `HexLayout.Size`), with a fallback for an unknown id.
- `Client/Features/Games/Helpers/BoardSceneBuilder.cs`: builds the four new lists, reusing `HexLayout.EdgeEnds`, `HexLayout.BridgeEnds`, the corner offset and visibility rules of `BoardImprovements`, the edge rule and 0.12 inset of `BoardBorders`, and `Helpers/Seats.Number`. Split into a partial or a second builder class if it passes the 300-line or 40-line limits.
- `wwwroot/js/board3d/rivers.js` (new): river ribbons (a flat quad per edge, round discs at the corners so river segments join) and bridges.
- `wwwroot/js/board3d/models.js` (new): one small builder per `FeatureModel`, made from boxes, cones and cylinders as InstancedMeshes; seat colour on the roof or flag, opacity 0.45 when planned.
- `wwwroot/js/board3d/overlays.js` (new): border dashes as one InstancedMesh per seat.
- `wwwroot/js/board3d/terrain.js`: decorations skip the corner a feature stands in.
- `wwwroot/js/board3d/scene.js`: builds and disposes the new layers with the terrain (the whole scene is rebuilt on a new view, as now).
- `wwwroot/css/tokens.css`: a few model tokens in light and dark (`--gv-board3d-wall`, `--gv-board3d-roof`, `--gv-board3d-field`, `--gv-board3d-rock`, `--gv-board3d-canvas`); rivers, bridges and seats reuse `--gv-river*`, `--gv-bridge` and `--gv-seat-N`.

## Steps
1. Client models — `FeatureModel`, the four `Scene*` records, `BoardScene` grows its lists (empty lists keep step 1's scene valid).
2. Client helpers — `RiverReliefs`, `FeatureModels`, and the four builders in `BoardSceneBuilder`:
   - Rivers: every known river edge (visible and explored tiles, as `BoardRivers`), once per edge; the water sits just above the lower of the two tile tops (the higher tile's side makes the bank); an edge on the map's rim uses its one tile.
   - Bridges: every bridged edge in sight (as `BoardRivers`), from 0.45 of the way inside one tile to 0.45 inside the other, at both tile heights, so the deck slopes between them.
   - Features: improvements in sight, buildings in sight with their territory owner's seat, and each of the player's cities' `ProducingBuilding` on its `ProducingBuildingSite`, planned (as `BoardImprovements`); placed at the upper-left offset `BoardImprovements` uses (0.45 of a hex size), at the tile's height.
   - Borders: every edge of an owned tile whose neighbour has another owner, no owner or is off the map (as `BoardBorders`), inset 0.12 towards the owned tile, at its height.
3. JS — `rivers.js`, `models.js`, `overlays.js`; `scene.js` wires them; `terrain.js` leaves the feature corner free. Colours only through `palette.js` (`Palette.token`, `faded` for explored rivers).
4. Tokens — the model tokens in `tokens.css`, light and dark.

## Tests
- Unit: none (client only; Core does not change).
- Integration: none (no server change).
- Client (bUnit): `BoardSceneBuilderTests` per layer: a river edge appears once with its width, tone and the lower height, explored rivers stay and unexplored ones don't; a bridge in sight appears, one out of sight doesn't; an improvement and a building in sight appear with their model and seat, out of sight they don't, the city's producing building appears planned; borders run only along territory edges, in seat number, never between two tiles of one owner. `FeatureModelsTests` and `RiverReliefsTests`: every ruleset improvement, building and river has an entry (like `TerrainReliefsTests`). `GameBoard3DTests`: the rendered scene carries the new lists.
- Architecture: none new; `GameUiTests.Board3DScripts_TakeColoursOnlyFromTokens` already covers the new JS files.
- Manual: Playwright screenshots of rivers with a bridge, a farm, a mine and a city's buildings, two touching territories, light and dark, 1400 and 390 px, in `/mnt/project-files/screenshots/f15-3d-board/step2/`.

## Docs & rules
- `docs/features/game-ui.md`, "3D board (preview, F15)": rivers, bridges, improvements, buildings and borders in 3D, `FeatureModels` and `RiverReliefs`, and how a new improvement, building or river kind gets its 3D model.
- `.claude/rules/game-ui.md`: "Every ruleset id has its UI" adds the 3D model (`FeatureModels`) for a new improvement or building and the 3D width (`RiverReliefs`) for a new river kind.
- `docs/README.md` index; the [F15 plan](F15-3d-board.md) step 2 links here; the backlog F15 row says step 2.
- At merge (in the step 2 PR): this plan moves to `docs/plans/done/`.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes` and `dotnet test Govern.slnx` green.
- Run the Api and client, play to a map with rivers, a bridge, a farm, a building and both territories, turn the 3D board on, and compare it with the 2D board in Playwright screenshots; the tile under a river or bridge still selects on click.

## Risks & open questions
- **Rivers between tiles of different heights:** a flat ribbon at the lower tile's top runs against the higher tile's side, which reads as a bank. If it looks wrong next to mountains, the ribbon gets a small slope instead; no rule change either way.
- **Small models at a far zoom:** at the widest zoom a farm or a tent is a few pixels. The seat colour and the model's outline carry it; step 3's banners don't apply to features. If they vanish, step 5 can scale features with the zoom.
- **Planned buildings and transparency:** faded models with `depthWrite` off can sort badly against the mist; planned ones are only on the player's own tiles, which are never under the mist.
