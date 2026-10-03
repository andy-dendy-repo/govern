---
title: "Plan: F19 unit auto moves (walk to a destination over several turns)"
tags: [plan, game, units, movement, orders, client]
code: [src/Govern.Core/Actions, src/Govern.Core/Map, src/Govern.Core/Units, src/Govern.Client/Features/Games]
updated: 2026-10-03
---

# Plan: F19 unit auto moves

## Context
Andrii asked for "auto moves" (2026-10-03). Today a unit only moves to a tile it can reach this turn, so a long walk is a click on every unit every turn. The default reading: a unit can be given a destination anywhere on the map the player has explored, and it keeps walking there on its own, turn after turn, until it arrives, is blocked, or the player gives it another order.

Alternatives considered and not planned (one line each, for the owner to pick instead): automatic exploring (the unit walks to the nearest unexplored tile by itself); patrol between two tiles; "sentry" (skip the unit until an enemy comes into sight). They can reuse the same order field later.

## Scope
- In: a destination order on a unit; walking it when the order is given and at the start of each of the owner's turns; stopping rules; a log line when a walk ends; a "Go to" mode in the unit card, the path and destination marked on the board, a "Heading to" line with Cancel.
- Out: exploring, patrol, sentry; a turn-count label on every tile of the path (the destination shows the number of turns only); orders for groups of units.

## Rules (Govern's own, not Civ's)
1. **Giving the order.** `SetDestinationCommand(UnitId, Target)` on an own unit. The target must be a tile the player has explored, passable, not the unit's tile, with a path over explored tiles. The unit drops its job (as moving does) and walks at once with the movement it has left.
2. **The path.** The cheapest path by total movement cost over tiles the player has explored, through no unit or city the player can see blocking it, and no enemy city; steps over rivers priced as today. Planned from the player's knowledge only (what `PlayerView` holds), so the path never reveals hidden units or terrain. Re-planned every time the unit walks, so new knowledge (a bridge, a seen blocker) is used.
3. **Walking.** Step by step along the path while the next step fits in the movement left (the same no-overspend rule as `MoveUnitCommand`). Each walk is one `UnitMovedEvent`, so the log and the move animation work unchanged.
4. **When.** When the order is given, and at the start of the owner's turn, after their movement is restored (inside `EndTurnRule.PassTurn`, so the owner sees where the unit got to before acting). Units walk in id order.
5. **Stopping.** The order ends, with a `UnitOrderEndedEvent(unit, owner, reason)` that only the owner sees, when the unit:
   - arrives (`Arrived`);
   - has no path any more (`Blocked`), e.g. the target became an enemy city;
   - ends a step with an enemy unit or city in its sight that it could not see before the walk started (`EnemySighted`), so the player decides what to do.
   A step into a tile taken by a unit the player could not see stops that walk for this turn but keeps the order.
6. **Cancelling.** Any other command on the unit (move, attack, build, found city, a new destination) replaces the order. `SetDestinationCommand(UnitId, null)` clears it.
7. **Visibility.** The order is shown only to its owner (`UnitView.Destination`, as with `Job`).

## Affected areas
- Core: `Units/Unit.cs` (`Destination`, left out of the JSON while null, so old saves load), `Actions/SetDestinationAction.cs` (command + rule), `Map/PathFinder.cs` (multi-turn cheapest path, reusing `ReachabilityCalculator.StepCost`), `Engine/AutoMoveRule.cs` (walk one unit, walk all of a player's), `Events/UnitOrderEndedEvent.cs` + `Units/OrderEndReason.cs`, `Actions/EndTurnAction.cs` (call it after `ResetMovement`), the other unit actions (clear `Destination`), `Engine/GameEngine.cs` (dispatch + preview `DestinationPath(ruleset, view, unitId, target)`), `Visibility/UnitView.cs`, `Visibility/EventVisibilityCalculator.cs` (owner only).
- Client: `Helpers/BoardClickResolver.cs` (a `Destination` mode like bridge mode; a click on an explored tile sends the command), `Models/BoardSelection.cs`, `Models/BoardHighlights.cs` (path tiles + destination), the WebGL board overlay for the path, `Components/UnitPanel.razor` ("Go to", "Heading to … (N turns)", Cancel), `Helpers/GameLogFormatter.cs`, `Helpers/TileCardBuilder.cs` (the destination tile says so).
- No ruleset change, no server code change (commands pass through as today), no migration.

## Steps
1. Core: `Unit.Destination`, `PathFinder`, `SetDestinationCommand` + rule, `AutoMoveRule`, end-of-turn hook, the other actions clearing the order, event + visibility, preview.
2. Client: selection mode, highlights and board path, unit panel, log line, tile card.
3. Docs: game logic, rules reference, game UI, game-core feature doc, backlog close-out.

## Tests
- Unit (Core): path over explored tiles only; path avoids visible units and enemy cities, ignores hidden ones; set destination walks at once and drops the job; walk continues at the next turn start; arrival, blocked and enemy-sighted end the order with the event; hidden blocker stops the walk but keeps the order; other commands clear it; invalid targets (unexplored, impassable, own tile, enemy's unit) rejected; serializer round trip and old JSON without the field; event hidden from the opponent; `UnitView` hides the opponent's destination.
- Client (bUnit): "Go to" mode, click sends `SetDestinationCommand`, highlights show the path, panel shows "Heading to" and Cancel sends the null command, log line text.
- Integration: one command round trip through `SendGameCommandEndpoint` (happy path, 400 for an invalid target).
- Architecture: none, existing rules cover it.

## Docs & rules
- `docs/game/game-logic.md` (orders section), `docs/game/rules.md` (player rules), `docs/features/game-ui.md`, `docs/features/game-core.md`.
- `docs/plans/backlog.md`: F19 row; close-out in the same PR.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green; manual run in the browser (Playwright): give a soldier a far destination, end turns, watch it walk and stop.

## Risks & open questions
- The reading of "auto moves" itself (above); the alternatives can be added later on the same field.
- Walking inside the opponent's End turn command means the opponent's update carries the owner's moves; the existing per-viewer event filter already hides what the opponent can't see.
