---
title: "Plan: G18 predefined maps (the map pool, first map The Basin)"
tags: [plan, done, game, maps, map-pool, predefined-map, basin, lobby, map-choice, civ6-ideas]
code: [maps, src/Govern.Core/Map, src/Govern.Core/State/GameFactory.cs, src/Govern.Domain/ValueObjects/MapChoice.cs, src/Govern.Application/Games/Services/MapPoolService.cs, src/Govern.Client/Features/Games/Services/MapPoolService.cs]
updated: 2026-10-03
---

# Plan: G18 predefined maps (the map pool, first map "The Basin")

**Status: done 2026-10-03 (PR #55).** Owner's ask (2026-10-03): plan a pool of predefined maps, because generated maps are small and shapeless; look at the most interesting Civ 6 map types and create one map for now. Owner's decisions: a lobby picker (Random from the pool by default, a named map, or Generated); size 32 × 20.

## Context
Every game was played on a generated 20 × 14 map (280 tiles): the generator scatters patches of terrain at random shares and adds two short rivers, so there are no fronts, passes or regions worth fighting over. This adds a pool of hand-made maps and its first map, sized for 1v1; more maps are later content, one data file each.

## What the Civ 6 map types offer (ideas only, not a template)
Govern has land units only (ships are G15), lakes and mountains are impassable, and rivers sit on hex edges. Water-heavy types (Continents, Island Plates, Archipelago, Small Continents) don't fit yet.

| Civ 6 type | The one idea worth taking for Govern |
|---|---|
| Inland Sea | A big impassable centre splits the front into two flanks |
| Highlands | Chokepoints and passes held from hills (+1 sight, +3 defence) |
| Lakes | Scattered water sources (lake 3 water) as expansion targets |
| Tilted Axis | Regions with a character: wood-rich, stone-rich, a barren desert band |
| Mirror | Fairness; Govern already mirrors every map by a half turn |

Size reference: Civ 6's duel map is 44 × 26. Govern's cities claim radius 1 growing to 3 and units move 1 to 4, so about 600 to 650 tiles (2.3× before) gives each player room for 4 to 6 cities; 32 × 20 = 640.

## Scope
- In: the map file format, the Core parser and validators, `GameFactory` on a predefined map, the embedded pool on the Api and the client, the lobby's map choice (`MapChoice`, column `games.map`), the map name in the lobby, The Basin, a map preview page `/design/maps`.
- Out: more maps; a map editor; lobby previews or thumbnails; resource deposits; generator changes (it stays as the Generated choice).
- Changed from the first draft: no `GET /api/games/maps` endpoint, because the client embeds the same `maps/*.json` (as it does the ruleset) and names maps itself; the preview is its own page instead of a picker on the board bench.

## Affected areas
- Core: `Map/PredefinedMap.cs`, `PredefinedMapParseResult.cs`, `PredefinedMapDocument.cs`, `PredefinedRiverDocument.cs`, `PredefinedMapParser.cs`, `PredefinedMapValidator.cs`, `PredefinedMapBalanceValidator.cs`; `State/GameSetup.cs` (`Map`), `State/GameFactory.cs`.
- Domain: `ValueObjects/MapChoice.cs`; `Entities/Game.cs` (`Map`, `Create(..., map)`, `Start(..., map)`).
- Infrastructure: `Persistence/Configurations/GameConfiguration.cs`, migration `AddGameMap`.
- Application: `Games/Services/MapPoolService.cs`; `Games/Contracts/CreateGameContract.cs` (`Map`, `CreateGameValidator`), `StartGameContract.cs`, `GetGameContract.cs` (`Map`); `Games/Models/GameErrors.cs` (`game.map_not_in_pool`).
- Api: `Features/Games/CreateGameEndpoint.cs` (optional body).
- Client: `Features/Games/Services/MapPoolService.cs`, `Models/MapOption.cs`, `Models/CreateGameRequest.cs`, `Clients/GamesClient.cs`, `Pages/GamesPage` (picker), `Pages/GamePage` (lobby line); `Features/Design/Pages/MapPreviewPage.razor`, `Helpers/MapPreviewBuilder.cs`.
- Content: `maps/basin.json`.

## Steps
1. Core: the document, strict parser and validators; `GameSetup.Map`; `GameFactory` plays the map as it is and draws a spawn candidate per player (the second takes a mirror, drawn on its own).
2. Domain: `MapChoice` (`generated`, `random`, a key), `Game.Map`, `Start` checks the map fits the choice and records the key.
3. Infrastructure: the `map` column (varchar 32, default `generated`), migration `AddGameMap`.
4. Application: `MapPoolService` (embedded, parsed once, named after their keys, never empty), the create validator, start resolving the choice.
5. Api: `POST /api/games` takes `{ map }` (optional).
6. Client: the pool service, the picker, the lobby line, the preview page.
7. Content: The Basin, written as one half and mirrored by a script.

## Tests
- Unit: `Core/Map/PredefinedMapTests` (valid document, every pool map valid, each broken rule at its path, lake wall and deep-river wall cuts); `Core/State/GameFactoryPredefinedMapTests` (tiles unchanged, map size wins, candidates and mirrors, independent draws, determinism, size and terrain guards); `Domain/ValueObjects/MapChoiceTests`; `Domain/Entities/GameTests` (map choice and start).
- Integration: `CreateGameEndpointTests` (no body is random; each choice stored; unknown, empty or upper-case key 400; 401); `StartGameEndpointTests` (basin, random and generated start on the right map and size; a key that left the pool 409).
- Client: `MapPoolServiceTests`, `GamesPageTests` (default random, a picked map), `GamePageTests` (lobby line), `GamesClientTests` (body), `MapPreviewBuilderTests`, `MapPreviewPageTests`.
- Architecture: none new; existing rules cover the files.

## Docs & rules
- New `docs/game/maps.md`; `docs/game/rules.md` ("Maps"), `docs/game/game-logic.md` (creating a game, predefined maps), `docs/features/games.md` (map choice, error, column, migration), `docs/features/game-core.md`, `docs/features/game-ui.md`, `docs/game/design-roadmap.md` (G18), `docs/README.md`, backlog.
- `CLAUDE.md`, `.claude/rules/project-map.md` (`maps/`), `.claude/rules/game-core.md` (predefined maps).

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` (all green), `dotnet format Govern.slnx --verify-no-changes`; The Basin on `/design/maps`.

## Risks & open questions
- A fixed map can be learned; fog of war and random spawn candidates keep openings varied, and more maps are the real answer.
- Hand-placed edge rivers are easy to get wrong; the validator checks their symmetry and connectivity.
