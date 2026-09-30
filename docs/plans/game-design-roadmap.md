---
title: Game design roadmap (future mechanics)
tags: [plan, roadmap, game, design, ui, rules, terrain, resources, rivers, seasons, cities, territory, governors, units, combat, builders, buildings, science, culture, tech-tree]
code: [src/Govern.Core, src/Govern.Client/Features/Games]
updated: 2026-09-30
---

# Game design roadmap

Where the game goes after the 1-vs-1 prototype (game Plans 1–5, [game delivery](done-game-delivery.md)): a much richer, Civilization-like game. This is a list of future plans, not a schedule. Each item becomes its own plan in `.claude/rules/planning.md` format when it is picked up, runs through `/develop-feature`, and branches from and PRs to `gamecore`. Smaller and operational work is in [next follow-ups](next-follow-ups.md).

Items are numbered **G1–G15** so they don't clash with the follow-up numbers. Suggestions added to the original wish list are marked *(suggested)*.

| # | Item | Depends on | Size |
|---|---|---|---|
| G1 | [UI design system and approaches](#g1-ui-design-system-and-approaches) | — | M |
| G2 | [Game rules reference](#g2-game-rules-reference) | — | S, then ongoing |
| G3 | [Data-driven catalogs and a new state schema](#g3-data-driven-catalogs-and-a-new-state-schema-suggested) *(in progress: [ruleset plan](ruleset.md))* | — | M |
| G4 | [Terrain: more tile types](#g4-terrain-more-tile-types) | G3 | M |
| G5 | [Resources and tile yields](#g5-resources-and-tile-yields) | G4 | L |
| G6 | [Rivers and bridges](#g6-rivers-and-bridges) | G4 | M |
| G7 | [City territory and growth](#g7-city-territory-and-growth) | G5 | L |
| G8 | [City stats and the production queue](#g8-city-stats-and-the-production-queue) | G7 | L |
| G9 | [Army units and combat](#g9-army-units-and-combat) | G3, G5 | L |
| G10 | [Builders and buildings (with upgrades)](#g10-builders-and-buildings-with-upgrades) | G7, G8 | L |
| G11 | [Science and culture trees](#g11-science-and-culture-trees) | G8 | L |
| G12 | [Civilizations and governors](#g12-civilizations-and-governors) | G8, G11 | L |
| G13 | [Colonies founded by governors (settlers removed)](#g13-colonies-founded-by-governors-settlers-removed) | G12 | M |
| G14 | [Seasons](#g14-seasons) | G5, G6 | M |
| G15 | [Ships and naval movement](#g15-ships-and-naval-movement) | G4, G6, G9, G10 | L |

**Suggested order:** G1 and G2 first, because every later item adds UI and rules. Then G3 as the foundation for the data. Then the map (G4 → G5 → G6), cities (G7 → G8), units (G9), builders (G10), ships (G15, which need harbours from G10), the trees (G11), civilizations and governors (G12 → G13), and seasons last (G14), since they modify almost everything before them.

## Decisions taken (2026-09-30)

| Question | Decision |
|---|---|
| Science and culture upkeep (G11) | Owning an unlocked node costs points every turn. When the player loses science or culture points (upkeep above income), the **latest unlocked nodes become disabled** |
| Naval play | Ships are in scope: [G15](#g15-ships-and-naval-movement). They are built in harbours and can sail rivers in state 2 and 3 |
| Victory conditions | Last player standing only, for now |
| Order | The suggested order stands; turn timers, simultaneous turns and more players don't move ahead of it for now |

## Rules for every item (cross-cutting)

These follow from how `Govern.Core` works today ([game-core](../features/game-core.md), `.claude/rules/game-core.md`), and every item must keep them:
- **Deterministic and pure.** New mechanics live in Core, with base class library only. Any randomness (tile growth, combat damage, map generation) comes from `DeterministicRandom` in the state, so a replay gives the same game.
- **Fog of war first.** Every new field is either visible to everyone, visible only to its owner, or visible only on currently visible tiles. Decide which before coding and filter it in `VisibilityCalculator.ViewFor`. Examples: an enemy city's queue, loyalty and yields are private; a resource may stay hidden until the tech that reveals it.
- **View previews stay exact.** Anything the client previews (moves, attacks, founding, building) keeps a parity test against `Execute`. A new terrain or river rule adds parity scenarios.
- **Schema versions.** Each item that changes the state bumps the JSON schema version and reads the previous one, or declares that old games can't be read (there are no production games yet).
- **Size.** The view is sent on every change. Keep per-tile data compact (enums and small ints) and watch the `PlayerView` size on the largest map.
- **Rules reference.** Each item updates the rules reference (G2) in the same PR.

## G1. UI design system and approaches

- **Why:** every item below adds screens, panels, numbers and icons. Without agreed approaches the game UI grows inconsistent and hard to use. Decide the patterns once, write them down, and follow them.
- **Deliverables:**
  - `docs/features/game-ui.md` (the design approaches) and a rule in `.claude/rules/blazor-client.md` (or a new `game-ui.md` rule), so future features follow them.
  - Shared components in `Components/` (or `Features/Games/Components/<Group>`), each with bUnit tests.
- **Approaches to decide:**

| Topic | Proposal |
|---|---|
| Screen layout | Fixed zones: a **top bar** (turn, season, treasury, science, culture, diplomacy), the **map** in the centre, a **right panel** for the selection (unit, city, tile), a **bottom action bar** for the selected unit, and **full-screen views** for large content (tech and culture trees, the city screen, governors) |
| Interaction | Always "select, then act"; a hover or long-press tooltip shows exact numbers; destructive actions (disband, remove a building, resign) confirm inline; no action happens on a single accidental click |
| Map lenses | Toggleable overlays instead of cluttering the base map: resources, territory, yields, loyalty, rivers and crossings, seasons |
| Visual language | One icon set (SVG sprite, self-hosted, CSP-safe) for resources, yields, units and buildings; one colour per seat; a terrain palette readable in light and dark themes; the SVG layer order is fixed (terrain → rivers → improvements → borders → cities → units → overlays) |
| Components | `YieldList` (icon + number rows), `ResourceBadge`, `ProgressBar` with "n turns left", `QueueList` (reorderable), `TreeView` for tech, culture and governor skills, `StatTable`, `CostLabel` (production + resources) |
| Client state | One per-game UI state service (selection, open lens, open screen) instead of passing everything through parameters, so panels stay small |
| Performance | Keep rendering per layer with `ShouldRender`; measure with a 60×40 map and move terrain to a single cached layer (or a canvas) if SVG gets slow |
| Accessibility and devices | Panel actions keyboard-reachable; tooltips reachable on touch; the layout wraps on narrow screens (map first, panels below) |

## G2. Game rules reference

- **Why:** the rules are spread over the code, [game-core](../features/game-core.md) and the plans. Players and developers need one place that says how the game works.
- **Deliverable:** `docs/game/rules.md`: the complete, current rules, written for players. It covers the map and terrain, movement, cities, production, combat, victory, and later every G item. Tables of numbers (unit stats, terrain costs, yields) come from the ruleset.
- **Keeping it true:**
  - Every rules change updates it in the same PR (add this to the Definition of Done in `CLAUDE.md` and to `.claude/rules/game-core.md`).
  - *(Suggested)* A test compares the documented tables with the ruleset, so the numbers can't drift.
  - *(Suggested)* Later, the client shows the same reference in-game (a "Civilopedia" screen), fed from the ruleset.

## G3. Data-driven catalogs and a new state schema *(suggested)*

- **Why:** dozens of terrains, resources, units, buildings and tech nodes are easier to balance as data than as C# switches.
- **Status:** in progress, as the loadable ruleset ([ruleset plan](ruleset.md), format in [game data](../game/game-data.md)).
  - **Done (Ruleset A):** Core reads every number, list and text from a ruleset document (`rulesets/default/v1.json`), validated at load time with JSON paths; the former `UnitCatalog`/`TerrainCatalog` and constants are gone.
  - **Next (Ruleset B):** rulesets stored per version in the database and each game pinned to one, so a running game keeps its rules after a balance change.
  - Each later item adds its section to the ruleset (and, where the state changes, to the state schema).

## G4. Terrain: more tile types

- **Requested:** forest, jungle, desert, mountain, meadow, sea, ocean, lake.
- **Suggested additions:** plains, tundra, snow/ice, hills, marsh/swamp, savanna/steppe, taiga, coast/beach, floodplains, oasis, reef, volcano, canyon.
- **Suggested model (as in Civ):** three layers per tile instead of one big list:

| Layer | Values |
|---|---|
| Base terrain | meadow (grassland), plains, desert, tundra, snow, coast, sea, ocean, lake |
| Elevation | flat, hills, mountains (impassable, or passable at a high cost) |
| Feature | forest, jungle, marsh, oasis, floodplains, reef, taiga, volcano |

- **Rules per tile:** movement cost, defence bonus, sight modifier (hills see further, forests block sight), passable by land or by sea, and the yields for G5.
- **Also:**
  - **Map generation:** seeded continents, climate bands (snow at the poles, desert near the equator) and coasts. Spawn spots should be fair.
  - **Naval movement:** land units cross water only aboard ships ([G15](#g15-ships-and-naval-movement)); water tiles are impassable for them otherwise.
- **Tests:** view-preview parity scenarios for each cost and blocker; the sight invariants in `RulesetValidator`, rechecked with hills and forests.

## G5. Resources and tile yields

- **Requested:** water, sea water, food, metals (gold, silver, etc.), wood, rock, coal, oil, uranium.
- **Suggested additions and grouping:**

| Group | Resources |
|---|---|
| Basic yields (every tile has some) | food, production (from wood and rock), fresh water, sea water |
| Building materials | wood, stone (rock), clay, sand |
| Strategic (units and buildings need them) | copper, tin, iron, horses, coal, oil, natural gas, saltpeter (gunpowder), aluminium, uranium, rare earths |
| Luxury (growth and loyalty) | gold, silver, gems, marble, salt, spices, silk, cotton, wine (grapes), furs, dyes, incense |
| Food sources | wheat, rice, maize, cattle, sheep, fish, crabs, whales, fruit, game (deer) |

- **Tile parameters (requested):** each tile has a **maximum production per resource** it holds, for example 3 food, 2 wood, 1 iron. What a city actually gathers depends on the worked tile, its buildings (G10) and the season (G14).
- **Decide:**
  - Does a resource deplete? Proposal: renewable resources (wood, food, fish) regrow, and mined ones (coal, oil, metals, uranium) have reserves that run out slowly.
  - Is it visible before a tech reveals it? Proposal: strategic resources are hidden until their tech (G11); the view filters them per player.
  - Is there trade? Trading resources between players needs diplomacy, which is not in this list (see the open questions).

## G6. Rivers and bridges

- **Model:** rivers run **along hex edges** (as in Civ), with flow from high ground to the sea.
- **River states (requested):**

| State | Land units crossing | Ships ([G15](#g15-ships-and-naval-movement)) |
|---|---|---|
| 1 | Crossed freely | Too shallow: can't sail |
| 2 | Crossing costs extra movement (for example all remaining movement, or +1) | Can sail along it |
| 3 | Can't be crossed without a bridge | Can sail along it |

- **Bridges** are built by builders (G10) on a river edge. Units cross a bridge as if the river were state 1.
- *(Suggested)* The state can change with the seasons (G14): frozen in winter (state 1), flooding in spring (state 3). A ship on a river that drops to state 1 is stranded until it rises again.
- **Decide:** whether ships can pass under a bridge on a state 3 river (proposal: yes, bridges are high enough).
- *(Suggested)* Rivers give fresh water (G5), extra housing (G8) and a defence bonus to the unit defending across the river.
- **Tests:** pathfinding and the view previews cross edges, not just tiles, which needs new parity scenarios.

## G7. City territory and growth

- **Territory (requested):** a city owns tiles around it, up to a **maximum number of tiles** (or a maximum radius). New tiles are acquired over time at a speed driven by culture, as in Civ 6. Each time the growth counter fills, the city takes the "best" free adjacent tile, with weighted random choice from `DeterministicRandom`.
- *(Suggested)* A player can also buy a tile with gold; a territory lens (G1) shows ownership and the next tile.
- **Borders:** tiles of another player can't be taken by growth; enemy units in your territory could reduce your loyalty.
- **Visibility:** owned tiles are public (borders are visible on explored tiles), but the growth progress is private.

## G8. City stats and the production queue

- **City parameters (requested):** gathered amount of each resource, population, room for population (housing), loyalty, production, science points, culture points, diplomacy points.
- **Growth:** food surplus grows population until housing is full; population works tiles (automatically at first; *suggested* later, manual focus: food, production, science, culture).
- **Loyalty:** below a threshold the city produces less; at zero it rebels or flips to the player with the most pressure (Civ 6 style). Pressure comes from nearby cities, governors (G12) and luxuries (G5).
- **Production queue (requested):** a list of units, buildings and projects. Surplus production carries over; the first item that can't be built (no resources) is skipped or blocks (decide).
- **Visibility:** an enemy city shows its name, owner, population and defence, and nothing else.
- **UI:** a full-screen city view (G1) with yields, citizens, the queue and buildings.

## G9. Army units and combat

- **Unit parameters (requested):** type, health, moves per turn, attacks per turn, production cost, resource cost (G5). Defence is **computed from the attacker's type and the defender's health**.
- **Suggested roster (by era):**

| Class | Early | Later |
|---|---|---|
| Melee | warrior, swordsman | musketman, infantry |
| Anti-cavalry | spearman | pikeman |
| Ranged | archer | crossbowman, artillery |
| Cavalry | horseman | knight, tank |
| Siege | catapult | cannon |
| Recon | scout | — |

- **Suggested combat model:**
  - A class counter matrix (rock-paper-scissors): anti-cavalry beats cavalry, cavalry beats ranged, ranged beats melee from a distance, and siege beats cities.
  - Strength scales with health, and damage has a small random range from `DeterministicRandom`.
  - Terrain (G4) and rivers (G6) modify the defence.
  - A unit can't both move after attacking and attack twice, unless its stats allow it.
- **Ships** follow the same combat model, see [G15](#g15-ships-and-naval-movement).
- **Replaces** the current one-hit combat (an attack destroys the defender) and the "strength/HP combat" idea in the next follow-ups.
- **Previews:** the client shows the expected damage range before attacking. That preview must match the engine (a parity test over the damage range).

## G10. Builders and buildings (with upgrades)

- **Builder (requested):** a unit that lets a city apply its production to create, upgrade or remove buildings on tiles in its territory. It also builds bridges (G6).
  - *(Suggested)* A builder has a number of charges, or works for n turns per building. Decide which; Civ 6 uses charges.
- **Building parameters (requested):** health, room for living (housing), resources produced, production needed to build it.
- **Upgrades (requested):** some buildings upgrade into the next kind. *Suggested* chains:

| Chain |
|---|
| trading post → bazaar → market → bank → stock exchange |
| farm → irrigated farm → plantation |
| camp → lumber mill → sawmill |
| mine → deep mine → industrial mine |
| quarry → stoneworks |
| well → aqueduct → water tower |
| fishing boats → harbour → port (a harbour builds ships, [G15](#g15-ships-and-naval-movement)) |
| hut → house → apartment block (housing) |
| palisade → walls → fortress (defence) |

- **Health:** buildings can be damaged or pillaged by enemy units and repaired by builders.
- **Unlocks:** each building and upgrade is unlocked by a science or culture node (G11).

## G11. Science and culture trees

- **Requested:**
  - Each player has a **science tree** and a **culture tree**.
  - Science and culture points accumulate; reaching a node's cost spends them to unlock the node (a building, unit, resource, upgrade, etc.).
  - **Owning an item always consumes points:** each unlocked node has an ongoing upkeep in science or culture.
- **Upkeep rules (decided):**
  - Each turn: income minus the total upkeep of owned nodes. The rest accumulates towards the next node.
  - When the player **loses** points (the upkeep is higher than the income and the stored points run out), the **latest unlocked nodes become disabled**, newest first, until the upkeep of the enabled nodes fits the income again. A disabled node costs no upkeep.
- **To decide in the plan:**
  - *(Suggested)* A disabled node stays owned and is re-enabled automatically, oldest first, when the income covers its upkeep again, without paying its unlock cost again.
  - What "disabled" means for things the node unlocked. Proposal: they can't be built or started; existing units and buildings keep working, but lose the node's bonuses, and upgrades that need it stop.
  - The order when a science node and a culture node are both affected: each tree is balanced on its own points.
  - *(Suggested)* A later node can replace an earlier one, freeing its upkeep (for example, bronze working → iron working).
- **Visibility:** each tree is private; an opponent sees only what they can observe (for example, units they meet).
- **UI:** full-screen tree views (`TreeView`, G1) with cost, upkeep, prerequisites and what the node unlocks.

## G12. Civilizations and governors

- **Civilization (requested):** each player plays a civilization that has a set of governors. *(Suggested)* A civilization also has a unique bonus, and a unique unit or building.
- **Governors (requested):** each city has a governor. A governor has a **skill tree** that gives the city bonuses (production, loyalty, growth, defence, science…).
- *(Suggested)* Governors earn skill points over time (per turn in office, or per culture node), can move between cities (taking a few turns to settle in), and their number is limited by the civilization and the culture tree.
- **Fits the name:** Govern is about governing, so governors are the heart of the game design.

## G13. Colonies founded by governors (settlers removed)

- **Requested:** a city founds a new city (a colony) by **producing a new governor**, which travels to the site and founds it. The settler unit is removed.
- **Engine changes:**
  - `FoundCityCommand` is taken over by the governor unit.
  - `VictoryRule.CanStillPlay` checks "has a city or a governor that can found one" instead of `Definition.CanFoundCity`.
  - The start of the game gives each player a founding governor instead of a settler.
  - The view previews (`CheckFoundCity`) and their parity tests move to the governor.
- *(Suggested)* Producing a governor costs population, so expanding slows growth, as settlers do in Civ.

## G14. Seasons

- **Requested:** changing seasons.
- **Suggested model:**
  - A season lasts n turns (for example 4 seasons × 3 turns each = one year).
  - Seasons change yields (less food in winter, more in summer and autumn), movement (snow tiles cost more, frozen rivers become state 1, spring floods make them state 3), sight (fog in autumn) and unit attrition (units outside territory in winter lose health).
  - Seasons may differ by latitude: polar regions have longer winters.
  - The current season, and when the next starts, show in the top bar (G1), with a lens for its effects.
- **Tests:** season changes are part of the deterministic turn cycle, and view previews use the current season's costs.

## G15. Ships and naval movement

- **Ship parameters (requested):** type, health, room for units (how many land units it carries), and defence computed from the attacker's type and the ship's health, as for land units (G9).
- **Production (requested):** ships are built by cities with a harbour (G10: fishing boats → harbour → port). A port could unlock the bigger ship types.
- **Rivers (requested):** ships sail on sea, ocean, lake and coast tiles, and **along river lines in state 2 and 3** (G6). State 1 rivers are too shallow.
  - Rivers run along hex edges, so river sailing needs its own movement graph: along the river's edges and vertices, joining the water tiles where the river meets a lake or the sea. Pathfinding and the view previews must cover it, with parity tests.
- *(Suggested)* roster:

| Class | Early | Later |
|---|---|---|
| Transport | raft, galley | transport ship |
| Melee (boarding, ramming) | trireme | ironclad, destroyer |
| Ranged (bombarding) | — | frigate, battleship |
| Scouting | canoe (rivers) | — |

- **Carrying units:** units board a ship in a city or on an adjacent shore tile and unload on an adjacent land tile. Decide what happens to units aboard when the ship is destroyed (proposal: they are lost).
- **Fog of war:** units aboard an enemy ship are hidden; only the ship is visible.
- **Combat:** a counter matrix with land units too (for example ranged ships bombard coastal units and cities; melee land units can't attack ships at sea).

## Open questions

Answer before planning the item it affects:
1. **Diplomacy:** cities produce diplomacy points (G8), but there's no diplomacy system yet. Is diplomacy (war and peace, trade, alliances) a separate plan, or should diplomacy points be left out for now?
