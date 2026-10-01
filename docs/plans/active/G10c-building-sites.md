# Plan: G10c — buildings stand on a tile of the city's territory

## Context
Today a finished building (army camp, campus, trade post, culture centre, harbour) is only added to `City.Buildings`, so it sits "inside" the city tile. The owner wants each building placed on its own tile in the city's territory. Decisions made by the owner:
- The player picks the tile when ordering the building (pick-a-tile mode on the board, like bridge mode). The tile is reserved while the building is produced.
- Only free tiles qualify: no improvement, no other building, and not the city tile. Workers can't improve a building's tile or a reserved tile.
- The tile keeps its terrain and river yield, and the building's effects stay city-wide. Only the location changes.
- Saved games don't matter, so there is no state-schema upgrade.

## Scope
- In:
  - the building site on the command, the city (reserved) and the tile (built)
  - the placement rules and their view preview
  - improvements blocked on building and reserved tiles
  - the client pick-a-tile mode, building icons on the board, and log and animation at the site
  - docs and tests
- Out:
  - pillage or destruction of a building by enemy units standing on its tile (G10b)
  - moving or removing a placed building
  - any ruleset change: no new field is needed, so v7 stays as it is
  - an upgrade for old saves

## Affected areas

**Core:**
- `Map/Tile.cs`: a new `Building`.
- `Cities/City.cs`: a new `ProducingBuildingSite`.
- `Actions/ProduceBuildingAction.cs`: the command gains a `Site`; the rule and its checks change.
- `Actions/SetProductionAction.cs`, `Actions/AttackAction.cs` (`Capture`) and `Actions/EndTurnAction.cs` (`ProduceBuilding`).
- `Actions/BuildImprovementAction.cs` and `Engine/ImprovementSite.cs`.
- New `Engine/BuildingSite.cs`.
- `Engine/RuleViolations.cs`.
- `Events/BuildingCompletedEvent.cs`.
- `Visibility/TileView.cs`, `CityView.cs`, `VisibilityCalculator.cs` and `ViewPreviewCalculator.cs`.
- `Engine/GameEngine.cs`.

**Client** (under `Features/Games/`):
- `Models/BoardSelection.cs`, `Models/BoardHighlights.cs` and `Helpers/BoardClickResolver.cs`.
- `Services/GameUiService.cs` and `Components/CityPanel.razor(.cs)`.
- `Components/Board/BoardImprovements.razor` and `BoardPieces.razor`.
- `Helpers/GameLogFormatter.cs` and `Services/BoardAnimationService.cs`.

**Existing code to reuse:**
- The `ImprovementSite` and `CheckPlacement` pattern in `BuildImprovementAction.cs:64-86`.
- The bridge-mode flow:
  - `BoardSelection.Bridge`
  - `BoardClickResolver.Resolve`, `Prune` and `Highlights`
  - `GameUiService.ToggleBridge` and `Escape`
  - the `bridgeable` overlay in `BoardPieces.razor:12-25`
  - `GameEngine.BridgeTargets` (`ViewPreviewCalculator.cs:96-106`)
- `TerritoryRule.TilesOf`.
- `Icons.Building(id)`, which already has a symbol for every building in `GameSprite.razor`.

## Steps

### 1. Core: state
- `Tile` gets `BuildingType? Building = null`, written to JSON only when set. It is the building that stands on the tile.
- `City` gets `HexCoord? ProducingBuildingSite = null`, also written only when set. It is the tile reserved for `ProducingBuilding`, and is always set together with it.
- `City.Buildings` stays the city's list. Upkeep, damage, yields and production keep reading it unchanged.

### 2. Core: command and rule
`ProduceBuildingCommand(CityId CityId, BuildingType Building, HexCoord Site)`.

`ProduceBuildingRule` splits its check in two:
- **City check** (`Check(ruleset, city, building)`): returns `UnknownBuilding` or `BuildingExists`.
- **Site check** (`CheckSite(ruleset, definition, BuildingSite site)`):
  - The site is on the map, in this city's territory and not the city tile. Otherwise it returns a new `RuleViolations.NotInCityTerritory`.
  - The terrain is passable. Otherwise it returns `WrongTerrain`.
  - The tile has no improvement and no building, and is not reserved by the city for another building. Otherwise it returns a new `TileOccupied`.
  - No own unit has an improvement job on the tile. Otherwise it returns a new `TileBeingImproved`.
  - `RequiresAdjacentTerrain` is now checked against **the site's** neighbours, so a harbour must go next to a lake. Otherwise it returns `BuildingNeedsTerrain`.

`BuildingSite` is a new record in `Engine/BuildingSite.cs` holding terrain, improvement, building, whether the tile is in this city's territory, whether it is the city tile, whether it is being improved, and the neighbour terrains. `SiteOf(state, city, coord)` builds it from the state.

`Apply` behaves as follows:
- Same building and same site: no change.
- Same building, new site: moves the reservation and keeps the progress.
- Otherwise it works as today (refund, then switch) and also sets `ProducingBuildingSite`.

The event is unchanged.

### 3. Core: clearing the reservation
- `SetProductionAction` clears `ProducingBuildingSite` along with `ProducingBuilding`.
- So does `AttackAction.Capture`.

### 4. Core: completion
`EndTurnAction.ProduceBuilding` writes `tile with { Building = building }` at `ProducingBuildingSite` and clears the reservation.

The site can't become invalid while it is reserved:
- The reservation blocks improvements.
- Territory changes hands only with its city.
- A capture clears the reservation.

So completion needs no re-check. `BuildingCompletedEvent` gains a `HexCoord Position` (the site).

### 5. Core: improvements
- `ImprovementSite` gains `bool Occupied`: the tile has a building or is any own city's `ProducingBuildingSite`.
- `CheckPlacement` returns `TileOccupied` for it.
- `SiteOf` fills it from the state.
- The view preview (`ViewPreviewCalculator.CheckBuildImprovement`) fills it from `TileView.Building` and the own `CityView.ProducingBuildingSite`. A worker only builds in its own territory, so the viewer's own cities are enough.

### 6. Core: view
- `TileView` gets `Building`, shown on **visible tiles only**, the same as improvements. The decision is recorded in `game-core.md`.
- `CityView` gets `ProducingBuildingSite`, shown to the **owner only**, the same as `ProducingBuilding`.
- `VisibilityCalculator` fills both.

### 7. Core: previews
- `GameEngine.CheckProduceBuilding(ruleset, view, cityId, building)` stays and does the city check. When no tile qualifies, it returns the first site violation, so the panel can say "needs a tile next to a lake".
- New `GameEngine.BuildingSites(ruleset, view, cityId, building) → ImmutableArray<HexCoord>`: the own-territory tiles where `CheckSite` passes, ordered by row then column. It is built from the view: own territory is always visible, and its neighbours are at least explored, so their terrain is known.

### 8. Client
- `BoardSelection` gets `BuildingType? Placing = null`, with `City` selected.
- `BoardHighlights` gets `Placeable`, filled from `GameEngine.BuildingSites`.
- In the city panel, Build no longer sends the command. It calls `GameUiService.PlaceBuilding(ruleset, view, building)`, which enters placing mode, and the panel shows "Pick a tile for the <building>".
- `BoardClickResolver.Resolve`:
  - A click on a placeable tile returns `ProduceBuildingCommand(city, building, coord)` and leaves the mode.
  - Any other click leaves the mode.
- `Prune` drops the mode when no sites are left or the city is gone, and `Escape` leaves it.
- `BoardPieces` draws the `placeable` overlay with the bridgeable style.
- `BoardImprovements` draws `Icons.Building(tile.Building)` on built tiles. On the own reserved site it draws a faded icon, from `CityView.ProducingBuildingSite`.
- The city panel's Producing line names the site, and a click on it selects the tile or centres the board on it, if that already exists.
- `GameLogFormatter` uses `BuildingCompletedEvent.Position`, and `BoardAnimationService` pulses that position.

## Tests

**Unit, Core:**

`Actions/ProduceBuildingActionTests`:
- happy path: the site is reserved and the progress is kept
- switching the site keeps the progress
- the same building and the same site change nothing
- a site that fails each check:
  - outside the territory
  - another city's territory
  - the city tile
  - a lake or mountain
  - an improved tile
  - a tile with a building
  - a tile being improved
  - a harbour with no lake next to the site, and a harbour site next to a lake while the city itself is not
- the existing cases: unknown id, already built, enemy city

`Engine/ConstructionRuleTests`:
- a finished building is written to its tile and the reservation is cleared
- capture clears the reservation, and the placed buildings stay on their tiles

`Actions/SetProductionActionTests`: switching to a unit clears the site.

`Actions/BuildImprovementActionTests`: `TileOccupied` on a building's tile and on a reserved tile.

`Engine/BuildPreviewTests`, the parity tests:
- `BuildingSites` and `CheckProduceBuilding` from the view equal `Execute` for every tile in the territory
- the improvement preview equals `Execute` on reserved and built tiles

`Visibility/VisibilityCalculatorTests`:
- a building on a tile is shown on visible tiles only
- `ProducingBuildingSite` is shown to the owner only

`Serialization/GameSerializerTests`: `Tile.Building` and `City.ProducingBuildingSite` survive a round trip.

**Client (bUnit):**
- `GameTableBuildTests`:
  - Build enters placing mode
  - clicking a highlighted tile sends the command with the site
  - clicking elsewhere cancels
  - a harbour with no site is disabled with its reason
- `BoardClickResolverTests`: the placing cases for `Resolve`, `Prune` and `Highlights`.
- The board draws the building icon on its tile.

**Integration:** none. The command endpoint is generic, and the existing game tests cover the round trip. If any integration test sends `ProduceBuildingCommand`, it gets updated.

**Architecture:** none. `RulesReferenceTests` still pass, because no table changes.

## Docs & rules
- `docs/game/rules.md`, the "City buildings" prose: the player picks a free tile in the territory, the tile keeps its yield, and a harbour must be next to a lake. Under "Improvements": not on a building's tile or a reserved one.
- `docs/game/game-logic.md`, "Produce a building": the site rules, the reservation and completion.
- `docs/game/game-data-economy.md`: `requiresAdjacentTerrain` now applies to the building's tile.
- `docs/features/game-core.md`: the new state and view fields, plus a JSON example.
- `docs/features/game-ui.md`: placing mode.
- `.claude/rules/game-core.md`:
  - line 30: `TileView.Building` is visible tiles only, `CityView.ProducingBuildingSite` owner only
  - line 31: add `BuildingSites` and `CheckSite` to the shared previews
  - line 34: buildings stand on a reserved tile of the territory
- Backlog:
  - add a G10c row with status `in progress` and its branch
  - write the plan to `docs/plans/active/G10c-building-sites.md`
  - on merge, move the plan to done and the row to Recently done
- G10 in `design-roadmap.md`: note that buildings now stand on territory tiles.

## Verification
- `dotnet build Govern.slnx`, with no warnings.
- `dotnet test Govern.slnx`: everything green, with the summary shown, through `/verify`.
- `dotnet format Govern.slnx --verify-no-changes`.
- Manual check:
  1. Run the Api and the client, start a game, found a city and wait for some territory.
  2. Open the city panel, go to Buildings and click Build campus. The free territory tiles are highlighted.
  3. Click one. The faded icon appears there.
  4. End turns until the campus finishes. Its icon stays on that tile, the log points there, and a builder there can't start a farm.
- Use Playwright screenshots to confirm it.

## Risks & open questions
- Stored game logs that contain a `building_completed` event without `position` will fail to deserialize. Old local games may stop loading, which the owner accepted ("don't care about saved games").
- A city with a small territory and every tile improved can't place a building until it grows. The panel shows the reason, and the rules reference says so.
- A building can sit on a tile that a unit stands on, own or enemy, and this has no effect yet. Pillage stays in G10b.

## Decisions during implementation
- New violation codes: `city.building_not_in_territory`, `city.building_wrong_terrain`, `city.building_site_taken`, `city.building_site_being_improved` and `build.tile_has_building`. The `city.building_needs_terrain` message now says "next to its tile".
- `CheckProduceBuilding` reports the reason of the tile that passed the most site checks, so a harbour without a lake says it needs one.
- The Build button reads "Move <building>" for the building in production, and a click re-picks its tile.
- A building in production with no site (state from before G10c) still finishes; it simply has no tile.
- A tile's building is drawn in its owner's seat colour, like cities and units. The building symbols are filled shapes, so a plain icon colour left them black.
- Starting to place a building moves keyboard focus back to the game screen. Radzen's tabs keep Esc from bubbling up from the panel's buttons; this was found in the Playwright check.
- A click on the building's current site only stops placing, with no command sent.
