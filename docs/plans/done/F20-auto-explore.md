---
title: "Plan: F20 auto-explore, with the builder lock, WASD camera, richer tiles and rivers, and an asymmetric Basin"
tags: [plan, game, units, movement, orders, client, board3d, maps]
code: [src/Govern.Core/Actions, src/Govern.Core/Engine, src/Govern.Core/Map, src/Govern.Core/Visibility, src/Govern.Client/Features/Games, src/Govern.Client/wwwroot/js/board3d, maps]
updated: 2026-10-04
---

# Plan: F20 auto-explore (and the polish bundle)

Status: done 2026-10-04 (PR #61). The owner asked for six changes in one PR (2026-10-04); the plan-for-go step was skipped at the owner's request.

## Context
Andrii asked for: a builder at work that can't be moved by a misclick, with a cancel button; camera panning with WASD; richer tile textures with variants; a better river texture; The Basin no longer the same on both sides; and auto-explore (F20, deferred since F19). All six ship together.

## Scope
- In: `ExploreCommand` and `Unit.Exploring`; `CancelJobCommand` and the builder lock (`unit.busy`); WASD panning; textured hex tops and decoration variants; winding rivers with banks; an asymmetric Basin with per-side spawn lists and measured fairness.
- Out: arrow keys (they belong to focused controls), sea exploration rules beyond what the path finder already allows, a separate "sentry" order, real image textures (everything is generated geometry and vertex colour, nothing to license).

## Affected areas
- Core: `Actions/ExploreAction.cs`, `Actions/CancelJobAction.cs`, `Actions/MoveUnitAction.cs`, `Actions/SetDestinationAction.cs`, `Engine/AutoMoveRule.cs`, `Map/ExploreFinder.cs`, `Visibility/ExplorePreviewCalculator.cs`, `Map/PredefinedMap*.cs`, `State/GameFactory.cs`, `Units/Unit.cs`, `Visibility/UnitView.cs`, `Engine/RuleViolations.cs`.
- Map: `maps/basin.json` (`firstSpawns`, `secondSpawns`).
- Client: `Games/Components/Screen/ActionBar.razor`, `Games/Components/UnitPanel.razor`, `GameUiService`, `GameLogFormatter`, `wwwroot/js/board3d/keys.js`, `scene.js`, `terrain.js`, `rivers.js`, `css/tokens.css`, the map preview.

## Steps
1. Core: the builder lock. `MoveUnitRule` and `SetDestinationRule` refuse a unit with a job (`unit.busy`); `CancelJobCommand` drops it (`unit.no_job` when there is none); previews give such a unit no reachable tiles.
2. Core: exploring. `ExploreCommand` sets `Unit.Exploring` and walks at once; `AutoMoveRule` walks explorers at the start of the owner's turn to the nearest known tile next to a never-seen one (`ExploreFinder`, Dijkstra over the owner's knowledge), ending with `UnitOrderEndedEvent` (`Explored` or `EnemySighted`); every other order clears it; `GameEngine.CheckExplore` gives the same answer from the view.
3. Core: maps. `PredefinedMap` gets `FirstSpawns` and `SecondSpawns`; the mirror checks become measured fairness (per-half resource yields within 15%, each start within 10% of the mean, sides within 5%); The Basin is redrawn with different halves.
4. Client: Cancel job replaces Go to for a builder at work; Explore and Stop in the action bar; the exploring line in the unit card; explorers aren't idle; the log line.
5. Client 3D: WASD (physical keys, Shift faster, ignored in inputs) pans the camera; tile tops get facets and noise patches; plains, forests, hills, mountains, desert and lakes get several decoration variants; rivers wind, shade at the sides and sit on a bank.

## Tests
- Unit: `ExploreActionTests` (walks now and later, ends when nothing is left or an enemy is sighted, refusals, other orders stop it, preview parity, view privacy, JSON), `CancelJobActionTests`, builder lock cases in `ConstructionRuleTests` and `SetDestinationActionTests`, `PredefinedMapTests` (fair but different halves, a poor start reported), `GameFactoryPredefinedMapTests`.
- Client (bUnit): Explore sends, is disabled with nothing left, exploring shows Stop; a builder at work ignores a board click and Cancel job sends; the map preview counts both sides.
- Architecture: none; existing rules cover it.

## Docs & rules
- `docs/game/rules.md` (Exploring, the builder lock, maps), `docs/game/game-logic.md`, `docs/game/maps.md`, `docs/features/game-core.md`, `docs/features/game-ui.md`.
- `.claude/rules/game-core.md`: orders, building and predefined maps bullets.

## Verification
`dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx` all green; the client in headless Chromium shows the asymmetric Basin, the new tiles and rivers, and WASD panning without console errors.

## Risks & open questions
- The richer tiles raise the board to about 250,000 triangles; phone GPU numbers still need a real phone.
