---
title: "Plan: G5 tile yields, G7 city territory, G8a city stock, costs and upkeep"
tags: [plan, game, core, economy, tile-yields, territory, city-stock, water, desert, culture, resource-cost, upkeep, supply, shortage, building-health]
code: [rulesets/default/v6.json, rulesets/default/v7.json, src/Govern.Core/Engine/UpkeepRule.cs, src/Govern.Core/Economy/UpkeepCalculator.cs, src/Govern.Core/Economy/YieldCalculator.cs, src/Govern.Core/Engine/TerritoryRule.cs, src/Govern.Core/Engine/ConstructionRule.cs]
updated: 2026-10-01
---

# Plan: G5 tile yields, G7 city territory, G8a city stock, costs and upkeep

**Status:** done 2026-10-01: PR 1 (G5, G7) merged as #34, PR 2 (G8a, ruleset v7) is #35. Approved by Andrii 2026-10-01 ("Lets leave it free" after the questions below). Built around the owner's model (2026-10-01): every produced item has a production and a resource cost and a resource upkeep; a city keeps up what stands on its territory; a shortage costs HP; units outside territory live off the tiles around them; bridges have no upkeep. G10b (upgrades, pillage, repair) stays deferred.

## Context
G10a (PR #33, merged) left two stand-ins: a city's "work area" is the ring next to it, and resources pile up in a per-player stockpile that nothing spends. This slice gives every tile a default yield (G5), turns the work area into real, growing territory (G7), and gives each city its own resource stock that pays for what it produces and keeps up (the economy part of G8). Resources and buildings start to matter, and an army far from home has to be placed where the land can feed it.

## Scope
- In:
  - **Tile yields (G5):** every terrain has default yields per resource. A river on any edge of a tile adds water. An improvement adds its yield on top (farm, lumber camp, mine as in v5).

    | Terrain | Default yield |
    |---|---|
    | Plains | 2 food, 1 water |
    | Forest | 1 food, 2 wood, 1 water |
    | Hills | 1 food, 2 stone |
    | Mountains | 2 stone |
    | Lake | 1 food, 3 water |
    | **Desert (new)** | nothing |
    | Any tile with a river edge | +1 water |

  - **New resource `water`** and **new terrain `desert`** (passable, movement 1, generated in larger patches so a deep desert exists).
  - **City territory (G7):**
    - Each tile has at most one owning city. A new city claims its own tile and the unowned tiles within 1 (as today's work area).
    - The city's tile also yields 1 culture (`cities.yields`). Territory grows by **spending the city's culture**: when the stock reaches the next tile's price (4, +2 per tile already gained), the city pays it and claims one tile.
    - The tile is chosen deterministically, no randomness: an unowned tile (lakes and mountains included) next to the territory and within radius 3 of the city, with the highest total yield, then nearest to the city, then lowest row, then lowest column. The client can show "next tile" exactly.
    - Territory replaces the work area everywhere: improvements are built on own territory, bridges can't touch enemy territory, a captured city keeps its tiles.
  - **City stock (G8a):** resources belong to a **city**, not the player. Each turn a city adds the yields of every tile it owns plus its buildings' yields. The top bar shows the player's total; the city panel shows the city's stock, income, upkeep and net per turn. A new city starts with 5 food, 5 water, 5 wood, 5 stone (`cities.startingStock`) so it isn't stuck.
  - **Costs (owner's correction, 2026-10-01):** units and buildings keep their production cost and gain a resource cost (`resourceCost`), paid in full **up front, when production begins**. The player picks the item; at the end of that turn the city takes the whole resource cost from its stock and starts adding production from then on until the item is done. If the city can't pay yet, the item waits with no production added and the city tries again each turn. The city remembers that the item is paid (`City.ProductionPaid`), so a unit that repeats (the city produces the same unit again) pays again when the next one begins. Switching to another item before it is done refunds the paid cost to the stock. On capture, the paid cost is lost with the progress. Improvements and bridges have no resource cost (owner, 2026-10-01: "leave it free").
  - **Upkeep:** units and buildings have a per-turn resource upkeep (`upkeep`). Bridges and improvements have none.
    - **On own territory** the owning city pays, from its stock as a whole (tile yields under the unit don't matter there). An item is paid fully or not at all; an unpaid item takes nothing from the stock.
    - **Upkeep priority, a per-city switch (owner, 2026-10-01):** *buildings first* (default: buildings in ruleset order, then units) or *units first* (units, then buildings). Units are always paid lowest health first (ties by id). A new command `set_upkeep_priority` sets it; the city panel shows the switch. Kept on capture.
    - **Outside own territory** (neutral or enemy land) a unit is supplied by the 7 tiles around it, its own included: for each resource of its upkeep, the sum of those tiles' yields must reach it. Two units sharing tiles both get the full amount; nothing is split or used up.
  - **Shortage and recovery (HP):**
    - An unsupplied unit loses 10 HP that turn instead of healing (`upkeep.shortageDamage`), but **never below 1 HP** (owner, 2026-10-01): shortage alone never kills, it leaves the unit easy to finish off. A supplied unit heals by today's rules.
    - Buildings get HP (100). An unpaid building loses 10 HP; a paid, hurt one regains 10. A building at 0 HP stays but gives no yield and no production until it recovers. Destroying, pillaging and repairing buildings stay in G10b.
  - **Default upkeep and costs** (tuning numbers, all in the ruleset):

    | Item | Resource cost | Upkeep per turn |
    |---|---|---|
    | Settler | 4 food, 2 water | 1 food, 1 water |
    | Soldier | 2 food | 1 food, 1 water |
    | Spearman | 2 wood | 1 food, 1 water |
    | Horseman | 3 food, 2 water | 2 food, 2 water |
    | Archer | 2 wood | 1 food, 1 water |
    | Builder | 2 food | 1 food, 1 water |
    | Army camp | 4 stone, 2 wood | 1 food |
    | Campus | 4 stone | 1 gold |
    | Trade post | 4 wood | none |
    | Culture centre | 3 stone | 1 gold |
    | Harbour | 6 wood | none |
    | Improvements, bridges | none | none |

    With these numbers a soldier on plains or forest is always supplied; on bare hills or mountains it needs a river, lake or greener neighbour; in deep desert it starves.
  - **Ruleset v6 and v7** (PR 1 publishes `rulesets/default/v6.json` with the yields, territory and stock; PR 2 adds `v7.json` with costs and upkeep, since a merged version is never edited): `water`, `desert`, `terrains[].yields`, `rivers[].yields` (water), `cities.yields`, `cities.startingStock`, `territory` (`initialRadius`, `maxRadius`, `tileCost`, `tileCostIncrease`), `units[]`/`buildings[]` `resourceCost` and `upkeep`, `upkeep.shortageDamage`, `buildings.maxHealth`/`recovery`, texts. `cities.workRadius` is replaced by `territory.initialRadius`.
  - **Fog of war:** own territory is always in the own view; enemy borders show on visible tiles (like improvements). City stocks are private; building HP is private. Tile yields come from terrain and rivers, so the client derives them from the ruleset.
  - **Client** (only what these mechanics need):
    - Territory borders on the board for both players.
    - A **yields lens** on the existing lens toolbar (each tile's yield icons), so a player can see where an army can live.
    - Unit panel: the unit's upkeep and whether it is supplied where it stands ("short of water: −10 HP next turn").
    - City panel: stock, income, upkeep and net; resource cost next to each item in the production picker (paid when it begins), "paid" on the item in production, and "waiting for 2 stone to begin" when blocked; buildings with HP; the next territory tile and the culture it needs.
    - Top bar: the player's total stock.
- Out (deferred):
  - **G8b (new backlog row, deferred):** population, housing, loyalty, worked tiles, the production queue, science and diplomacy points as city stats. Andrii's model doesn't need them yet; this slice gathers from every owned tile.
  - Buying tiles with gold, culture pressure, flipping enemy tiles.
  - Depletion of mined resources, hidden strategic resources, resource trade between cities or players (each city's stock is its own).
  - G10b: building destruction, pillage, repair, upgrades.
  - Seasons changing yields (G14).

## Affected areas
- Ruleset: `rulesets/default/v6.json` (new).
- Core:
  - `Rulesets/` records for terrain/river yields, `TerritoryRules`, city yields and starting stock, `resourceCost`/`upkeep` on `UnitDefinition` and `BuildingDefinition`, `UpkeepRules`; `EconomyRulesValidator` checks.
  - `Map/Tile.cs` (+ `Owner` city id, left out of JSON when unowned); `Map/TileYield` calculator (terrain + river + improvement).
  - `Cities/City.cs` (+ `Stock`, + building damage, + `UpkeepPriority`), `Actions/SetUpkeepPriorityAction.cs` (new command), `Cities/WorkArea.cs` → `Cities/Territory.cs` (claim, next tile, contains), `State/Player.cs` (`Resources` removed; migrated into the player's first city on read).
  - `Engine/`: `UpkeepRule` (paying, tile supply, shortage damage, recovery), `TerritoryRule` (growth), the end-turn production pays the resource cost when an item begins (`City.ProductionPaid`), refunds it on a switch.
  - `Actions/FoundCityAction.cs` (claim tiles, starting stock), `Actions/AttackAction.cs` (capture keeps tiles and stock), `Actions/EndTurnAction.cs` (new order below), `Actions/BuildImprovementAction.cs` / `BuildBridgeAction.cs` (territory instead of work area).
  - `Events/UnitStarvedEvent.cs` (HP lost to shortage), `Events/TerritoryGrewEvent.cs`, `Events/BuildingDamagedEvent.cs` (own player only).
  - `Visibility/`: tile owner, city stock, building HP, previews (supplied here?, next tile, can pay).
  - `Serialization/GameSerializer.cs`: schema 3; read schema 2 (player stockpile → first city, territory computed from cities).
- Application / client: embed v6 in `Govern.Application.csproj`, `DefaultRulesetService.cs`, `Govern.Client.csproj`, `RulesetService.cs`.
- Client: `Board/BoardTerritory.razor` (new layer), `Lenses/YieldLens.cs`, `UnitPanel`, `CityPanel`, top bar, `/design` gallery, desert sprite.
- Reuse: `ConstructionRule`'s city-production lookup, `CostLabel` for resource amounts, `IMapLens`/`LensToolbar`, `GameStateBuilder` and `TestRulesets.With`.

## Steps
End of turn becomes: heal or starve → jobs → **income** (tiles + buildings into each city's stock) → **upkeep** (buildings, then units) → **territory growth** → **production** (pay the resource cost when an item begins, then add production) → pass the turn.

Two PRs, each playable:
1. **PR 1, yields and territory (G5 + G7):** v6 data for yields, water, desert, territory and city stock; `Tile.Territory`, territory claim and growth; per-city stock with income; work area replaced; schema 3 migration; client borders, yield lens, city stock and next tile. Nothing is spent except culture.
2. **PR 2, costs, upkeep and HP (G8a), ruleset v7:** resource costs paid when production begins, upkeep, tile supply outside territory, shortage damage and recovery, building HP; client upkeep and supply in the panels, cost in the picker. Backlog close-out in this PR.

## Tests
- Unit (Core):
  - `RulesetTests`: new sections parse; each validator check fails with its path (yields 0..100, known resources, `initialRadius < maxRadius`, spacing > 2 × `initialRadius`, upkeep/cost amounts, texts for water and desert).
  - `TileYieldTests`: terrain + river + improvement; desert yields nothing.
  - `TerritoryRuleTests`: founding claims only unowned tiles; growth pays culture and picks the best tile by yield, distance, index; never past `maxRadius` or onto owned tiles; capture keeps tiles.
  - `UpkeepRuleTests`: city pays buildings then units by health; all-or-nothing per item; unit outside territory supplied by its 7 tiles, two units on the same tiles both supplied, desert unit starves; both priority modes order the payments; shortage −10 HP, never below 1; recovery once paid; building at 0 HP gives nothing.
  - `EndTurnActionTests`: income from all owned tiles; the resource cost is taken in full the turn production begins, then production accumulates; with too little stock the item waits with no progress and starts once it can pay; a repeated unit pays again; switching refunds; capture loses the paid cost.
  - Preview parity (`GameEnginePreviewTests`): supplied-here, next territory tile, can pay, against `Execute`.
  - `VisibilityCalculatorTests`: enemy borders only on visible tiles; enemy stock and building HP hidden.
  - `GameSerializerTests`: a schema 2 game reads with its stockpile in the first city and territory around each city.
- Integration: existing game tests on v6; one Api test that `set_upkeep_priority` is accepted and an unsupplied unit loses HP after end turn.
- Client (bUnit): `BoardTerritory` draws borders; yield lens marks; `UnitPanel` supply line; `CityPanel` stock, net, waiting-for-cost, next tile and the upkeep priority switch; top bar totals.
- Architecture: none new. `RulesReferenceTests` gains tables for tile yields and upkeep checked against v6.

## Docs & rules
- `docs/game/rules.md`: tile yields, territory, city stock, costs, upkeep, supply outside territory, shortage.
- `docs/game/game-data-economy.md` and `game-data.md` (v6 sections), `docs/game/game-logic.md` (end-turn order, territory growth, upkeep), `docs/game/design-roadmap.md` (G5/G7/G8 status, G8b split), `docs/features/game-core.md`, `docs/features/game-ui.md` (territory layer, yield lens).
- Backlog: G5, G7, G8a to Now; G8b added to Deferred. Close-out in PR 2.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: green, summary shown, on each PR.
- Play both seats against a Postgres container: watch territory grow, march a soldier into desert and see it lose HP, pick an army camp without enough stone and see it wait, then start once the stone is in; recover a starving unit. Screenshots in `/mnt/project-files/screenshots/g5-economy/`.

## Risks & open questions
Answered by the owner 2026-10-01: upkeep priority is a per-city switch (buildings first or units first); shortage stops at 1 HP; population, housing, loyalty and the queue are deferred to G8b.
- Improvements have no upkeep and no resource cost: free once built, like bridges (owner, 2026-10-01).
- Numbers are a first tuning; they all live in the ruleset and can change in a v7.
- Size: PR 1 is about G10a's size; PR 2 a bit smaller.
