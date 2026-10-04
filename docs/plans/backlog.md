---
title: Backlog (all open work, prioritised)
tags: [plan, backlog, priorities, roadmap, deferred, game, ui, ruleset, content]
code: [docs/plans, docs/game/design-roadmap.md]
updated: 2026-10-04
---

# Backlog

The one list of open work and the only place priorities are kept. Details live elsewhere and are linked: game mechanics in the [game design roadmap](../game/design-roadmap.md) (what each G item is), release, infrastructure and multiplayer items in [deferred follow-ups](deferred/follow-ups.md). How the backlog is kept is in `.claude/rules/planning.md` ("Backlog").

## Buckets and statuses

| Bucket | Meaning |
|---|---|
| **Now** | Being worked on, or next to start. At most 3 items, in order |
| **Next** | Agreed; starts when a Now slot frees. In order |
| **Deferred** | Decided not to do now. Each item has the date, the reason and a "revisit when" trigger |
| **Ideas** | Not agreed as wanted yet |

Status: `needs decision` (a question for the owner), `needs plan`, `planned` (a plan in `docs/plans/active/`), `in progress` (branch or PR), `blocked by <id>`.

Ids never change and are never reused: `F` for follow-ups, `G` for game design roadmap items, `C` for content slices.

## Now

Empty: G11a is done (see Recently done). The owner said the culture tree (G11b) goes next.

### Content slice C1: G2, G4, G9

Agreed 2026-09-30, done 2026-09-30 (PR #30). The ruleset stays as it is today: one embedded file, no database (see G3b under Deferred). New content goes into that file, the Core rules that read it, the [game data](../game/game-data.md) and [game logic](../game/game-logic.md) docs, and the client panels that show it. Each item gets its own plan in `docs/plans/active/`, in this order:

| # | Id | Item | Why now |
|---|---|---|---|
| 1 | G2 | [Game rules reference](../game/design-roadmap.md#g2-game-rules-reference): done 2026-09-30, PR #26, [plan](done/G2-rules-reference.md) | Small, and every content item updates it, so it should exist first |
| 2 | G4 | [Terrain: more tile types](../game/design-roadmap.md#g4-terrain-more-tile-types): done 2026-09-30, PR #28, [plan](done/G4-terrain.md) | The map is the base for everything else; a first set of base terrain, hills and forest changes play without new systems |
| 3 | G9 | [Army units and combat](../game/design-roadmap.md#g9-army-units-and-combat): done 2026-09-30, PR #30, [plan](done/G9-units-and-combat.md) | Health and a small roster with counters make fights interesting with today's cities; builds on G4's defence modifiers |

## Next

| # | Id | Item | Area | Size | Notes |
|---|---|---|---|---|---|
| 1 | G11b | Culture tree: like the science tree (points per turn from culture, items that take them every turn, newest switched off when short), unlocking culture content (governors, policies, territory) | game | L | Owner, 2026-10-04: culture goes next. Needs plan. Open: culture also pays for territory today ([G11](../game/design-roadmap.md#g11-science-and-culture-trees)) |
| 2 | F13 | Scale-out beyond one Api instance (Postgres `LISTEN/NOTIFY` as the SSE backplane) | server | M | Only needed with more than one Api instance. Planned: [plan](active/F13-scale-out-backplane.md) |
| 3 | F14 | Production database on Neon Postgres (free plan) instead of Azure Flexible Server, about $16/month less; no app code change | infra | S | Planned: [plan](active/F14-neon-postgres.md). Timing is the owner's call (now or with F1) |

## Deferred

| Id | Item | Deferred | Why | Revisit when | Detail |
|---|---|---|---|---|---|
| G3b | Rulesets in the database: stored versions, pinning each game, serving them, selectable setups in the lobby | 2026-09-30 | Adds a table, a repository, caching and an endpoint with no player-visible gain while there is one ruleset | A balance change would alter running production games, or admin publishing or a lobby ruleset choice is wanted | [plan](deferred/ruleset-b-store-and-pin.md) |
| F1 | Ship the game | 2026-09-30 | The game gets richer content first | The owner decides the game is ready to release | [F1](deferred/follow-ups.md#f1-ship-the-game) |
| F2 | Least-privilege database access | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F2](deferred/follow-ups.md#f2-least-privilege-database-access) |
| F3 | Monitoring and alerts | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F3](deferred/follow-ups.md#f3-monitoring-and-alerts) |
| F4 | Sign-up email inside the database transaction | 2026-09-30 | Goes with F5 | With F5 | [F4](deferred/follow-ups.md#f4-sign-up-email-inside-the-database-transaction) |
| F5 | Email verification | 2026-09-30 | Only matters once deployed | Before or together with F1 | [F5](deferred/follow-ups.md#f5-email-verification) |
| F7 | Invite code rotation (kick) | 2026-09-30 | Low risk before release | Before F1, or with F10 | [F7](deferred/follow-ups.md#f7-invite-code-rotation-kick) |
| F8 | Turn timers and absent players | 2026-09-30 | Prerequisite of F9 and F10, which are deferred | With F9 or F10 | [F8](deferred/follow-ups.md#f8-turn-timers-and-absent-players) |
| F9 | Simultaneous turns (Civ 6 style) | 2026-09-30 | Content and UI first | Alternating turns become the main complaint in play | [F9](deferred/follow-ups.md#f9-simultaneous-turns-civ-6-style) |
| F10 | More than two players | 2026-09-30 | Content and UI first | 1-vs-1 play is solid; after F8 and F9 | [F10](deferred/follow-ups.md#f10-more-than-two-players) |
| G8b | City stats, part b: population, housing, loyalty and the production queue. Split from G8 on 2026-10-01; G8a (costs and upkeep) is done | 2026-10-01 | The owner wants costs, upkeep and supply first (G8a) | G8a is done and cities need to grow or be managed | [G8](../game/design-roadmap.md#g8-city-stats-and-the-production-queue) |
| G10b | Builders and buildings, part b: improvement upgrades, pillage and repair (building health came with G8a; unit and building upgrades with G11a), removing an improvement, the buildings' own mechanics (faster army production; traders came with G17, ships from harbours with G15a). Split from G10 on 2026-10-01; G10a is done | 2026-09-30 | Not in content slice C1; G10a gives the base; the rest needs G7, G8 | The owner picks the next G item | [G10](../game/design-roadmap.md#g10-builders-and-buildings-with-upgrades) |
| G12 | Civilizations and governors | 2026-09-30 | Not in content slice C1; needs G8, G11b | C1 is done; then pick the next slice | [G12](../game/design-roadmap.md#g12-civilizations-and-governors) |
| G13 | Colonies founded by governors | 2026-09-30 | Not in content slice C1; needs G12 | C1 is done; then pick the next slice | [G13](../game/design-roadmap.md#g13-colonies-founded-by-governors-settlers-removed) |
| G14 | Seasons | 2026-09-30 | Not in content slice C1; modifies almost everything; last | C1 is done; then pick the next slice | [G14](../game/design-roadmap.md#g14-seasons) |
| F20 | Auto-explore: a unit walks on its own to the nearest unexplored tiles, turn after turn, reusing the F19 destination order | 2026-10-03 | The owner wants auto moves (F19) first and auto-explore later | F19 is in play, or scouts (G9b) need it | [F19 plan](done/F19-unit-auto-moves.md) |
| G15b | Ships, part b: carrying land units (boarding, unloading, losing them with the ship, hidden aboard), sea and ocean terrain with a water-heavy map, sailing rivers in state 2 and 3 (an edge graph), ships entering cities and bombarding them, more ship types. Split from G15 on 2026-10-04; G15a (warships on lakes) is done | 2026-10-04 | The owner's first ask was ships produced in harbours; G15a ships them on lakes | Ships on lakes are in play and need more room or transport | [G15](../game/design-roadmap.md#g15-ships-and-naval-movement) |
| G16 | Diplomacy (war and peace, trade, alliances, diplomacy points) | 2026-09-30 | Not needed for 1-vs-1 | F10. Resource trading between players came without diplomacy in G17 | [open question 1](../game/design-roadmap.md#open-questions) |

## Ideas

None yet. New ideas are added here as one line and promoted to Next or Now by the owner.

## Recently done

| Id | Item | Done | Plan |
|---|---|---|---|
| G15a | Ships, part a: the galley (melee, Sailing) and the bireme (ranged, Shipbuilding), produced only by a city with a harbour or port and launched on the lake next to it; ships sail water only, melee reaches only the same ground (ranged crosses the shore both ways), ships heal next to their harbour (ruleset v12, [rules](../game/rules.md#ships)) | 2026-10-04, PR #60 | [G15a](done/G15a-ships.md) |
| G11a | Science tree: science is a budget per turn (1 a city, campus 2, university 4), never stored; unlocking an item takes its science every turn, prerequisites too, and the newest items switch off when the cities make too little; 11 items lock the archer, horseman, trader, trade post and harbour and unlock four units that replace older ones (swordsman, pikeman, crossbowman, knight; old units upgrade in own territory), the workshop, and four building upgrades a builder makes in place (barracks, university, market, port); the science counter and full-screen tree (ruleset v11, [rules](../game/rules.md#science)) | 2026-10-04, PR #59 | [G11a](done/G11a-science-tree.md) |
| G17 | Trading: each city with a trade post sets a market (sell or buy food, wood, stone or gold, up to a cap per turn; water can't be traded), demand levels move with need and offer, traders from trade posts walk to another city's post to open a route (2 per city), propose a per-turn swap the host accepts (cancelling older deals to make room) or declines, and shuttle between the posts while the deal swaps goods at the end of the host owner's turn; killing the trader breaks the deal, capture ends the route. No money: gold is a resource (ruleset v10, [rules](../game/rules.md#trading)) | 2026-10-03, PR #56 | [G17](done/G17-trading.md) |
| G18 | Predefined maps: a map pool (`maps/<key>.json`, mirrored, connected, spawn candidates) the lobby picks from (Random by default, a named map, or Generated), and its first map, The Basin, 32 × 20: home valleys, ridges with hill passes and a central lake that splits the front ([maps](../game/maps.md)) | 2026-10-03, PR #55 | [G18](done/G18-predefined-maps.md) |
| F12 | Player-chosen city names: Found city asks for the name, prefilled with a random unused suggestion from the ruleset (v9, 30 names); 1 to 24 letters, digits, spaces, `'`, `-` or `.`, unique among the player's own cities. Removes the `City N` count leak ([rules](../game/rules.md#founding-a-city)) | 2026-10-03, PR #54 | [F12](done/F12-city-names.md) |
| F19 | Unit auto moves: Go to sends a unit to any explored tile; it walks there on its own, at once and at the start of each of the owner's turns, over the cheapest path the owner knows, until it arrives, has no known way, or sights an enemy; the board shows its route and turns, the unit card a Stop button ([game logic](../game/game-logic.md#set-a-destination-auto-moves)) | 2026-10-03, PR #53 | [F19](done/F19-unit-auto-moves.md) |
| G9b | The scout (G9's recon unit): class Recon, movement 4, sight 4, strength 8 with no attack (it defends but can't attack or capture), costs 1 Food and needs 1 Food a turn; ruleset v8, the KayKit hooded rogue on the board ([rules](../game/rules.md#unit-types)) | 2026-10-03, PR #52 | [G9b](done/G9b-scout.md) |
| F18 | Board tile card and WebGL context loss: a card in the board's corner for the tile under the mouse or last tapped (terrain, pieces, building or improvement with "being built", territory, rivers and bridges, the lens's sentences), the pointer cursor where a click acts, the clicked tile's card read out to screen readers, and a lost WebGL context reported and redrawn, with Reload after 5 seconds ([game UI](../features/game-ui.md#game-board)). The regression review's A1/A2 (animations and `HexLayout` in board units) stay a possible clean-up | 2026-10-02, PR #51 | [F18](done/F18-board-hover-card.md) |
| F17 | Retire the SVG board: the WebGL board is the only board, with no 2D/3D toggle; the SVG board, `board.js` and their tests are gone, the game table tests click through the renderer, and a browser without WebGL gets a message instead of a fallback board ([game UI](../features/game-ui.md#game-board)) | 2026-10-02, PR #50 | [F17](done/F17-retire-svg-board.md) |
| F16 | Full-screen game view: the board fills the window and the controls float over it (top strip with the title, counters, Log, Full screen, Resign and End turn; the selection card with the unit's actions bottom left; the attack forecast beside its target; the log drawer; notices), browser full screen, and a phone layout with bottom sheets ([game UI](../features/game-ui.md#game-screen-layout-full-screen-view)) | 2026-10-02, PR #49 | [F16](done/F16-full-screen-game-view.md) |
| F15 | 3D game board behind the 3D button: WebGL (Three.js) low-poly terrain with mist fog, rivers, bridges, improvements, buildings and borders, baked CC0 KayKit unit models with banners, the selection, highlights and lenses, the F11b animations, pinch to zoom and two-finger pan, rebuilding only the layers that change, and a 40×40 bench at `/design/board` ([game UI](../features/game-ui.md#game-board)). Retiring the SVG board was F17 (done) | 2026-10-02, PRs #41–#44, #46, #48 | [F15](done/F15-3d-board.md), steps [2](done/F15-step2-rivers-improvements.md), [3](done/F15-step3-pieces-selection-lenses.md), [3b](done/F15-step3b-unit-models.md), [4](done/F15-step4-animations.md), [5](done/F15-step5-themes-touch-performance.md) |
| G10c | Buildings stand on a tile of the city's territory: the player picks a free tile when ordering a building, it is reserved while produced, and the building stands there in its owner's colour; the tile keeps its yield and takes no improvement ([rules](../game/rules.md#city-buildings)) | 2026-10-01, PR #38 | [G10c](done/G10c-building-sites.md) |
| F11b | Board animations from the game log: units walk their path, attacks lunge with floating damage, destroyed units fade, new cities and builds pulse; a click or a newer view finishes them, reduced motion respected ([game UI](../features/game-ui.md#board-animations)) | 2026-10-01, PR #37 | [F11b](done/F11b-animations.md) |
| F11a | Game log: engine events filtered per player by visibility (`VisibilityCalculator.EventsFor`), stored per seat, sent in every snapshot and shown in the side panel ([games](../features/games.md#game-log)) | 2026-10-01, PR #37 | [F11a](done/F11a-game-log.md) |
| G8a | City stats, part a: resource costs paid in full when production begins (refunded on a change), upkeep for units and buildings paid by the city whose territory holds them with a per-city order switch, units outside territory supplied by the land within 1, shortage damage of 10 a turn down to 1 health, building health 100 (nothing at 0, 10 back per paid turn); bridges and improvements free (ruleset v7, [rules](../game/rules.md#costs-and-upkeep)) | 2026-10-01, PR #35 | [G5, G7, G8a](done/G5-G7-G8-economy.md) |
| G7 | City territory and growth: a city claims the tiles within 1, then buys one tile at a time with culture up to radius 3; each city keeps its own stock (ruleset v6, [rules](../game/rules.md#territory)) | 2026-10-01, PR #34 | [G5, G7, G8a](done/G5-G7-G8-economy.md) |
| G5 | Resources and tile yields, first part: every terrain yields, desert gives nothing, rivers give water (ruleset v6, [rules](../game/rules.md#tile-yields)) | 2026-10-01, PR #34 | [G5, G7, G8a](done/G5-G7-G8-economy.md) |
| G10a | Builders and buildings, part a: the builder; builders and soldiers build with their own build production plus the city's on its ground, no charges (owner's model); farm, lumber camp and mine in a city's work area; army camp, campus, trade post, culture centre and harbour; bridges over any river; the resource stockpile (ruleset v5, [rules](../game/rules.md#building-with-units)) | 2026-10-01, PR #33 | [G10a](done/G10a-builders-and-buildings.md) |
| G6 | Rivers along hex edges: stream, river (+1 to cross, +3 defence) and deep river (can't cross, +5 defence), mirrored river generation, the rivers board layer and lens crossings (ruleset v4, [rules](../game/rules.md)). No generated bridges (owner); bridges moved to G10 | 2026-10-01, PR #32 | [G6](done/G6-rivers.md) |
| C1 | Content slice C1: richer ruleset and Core (G2, G4, G9); its section stays under Now for the record | 2026-09-30, PR #30 | [slice](#content-slice-c1-g2-g4-g9) |
| G9 | Army units and combat: health, spearman, horseman and archer with class counters, terrain defence, healing, the combat forecast and health bars (ruleset v3, [rules](../game/rules.md)). Completes content slice C1 | 2026-09-30, PR #30 | [G9](done/G9-units-and-combat.md) |
| G4 | Terrain: forest, hills (+1 sight), mountains and lake, seeded mirrored map generation (ruleset v2), the movement-cost map lens ([rules](../game/rules.md), [game UI](../features/game-ui.md)) | 2026-09-30, PR #28 | [G4](done/G4-terrain.md) |
| G2 | Game rules reference for players ([rules](../game/rules.md)), checked against the ruleset by `RulesReferenceTests`; a rules change updates it (Definition of Done) | 2026-09-30, PR #26 | [G2](done/G2-rules-reference.md) |
| G1 | UI design system: screen zones, tokens and light/dark, icon set, board layers, shared components, `/design` gallery ([game UI](../features/game-ui.md)). Map lenses moved to G4 | 2026-09-30, PR #24 | [G1](done/G1-ui-design-system.md) |
| G3a | Core reads every game number from a loadable ruleset | 2026-09-30, PR #20 | [ruleset A](done/ruleset-a-core.md) |
| F6 | Hide a visible enemy unit's movement (`UnitView`) | 2026-09-30, PR #19 | — |
