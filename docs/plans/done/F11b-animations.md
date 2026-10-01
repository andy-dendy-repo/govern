---
title: "Plan: F11b board animations from game events"
tags: [plan, game, client, animations, board, svg, events, reduced-motion, game-ui]
code: [src/Govern.Client/Features/Games/Components/Board, src/Govern.Client/Features/Games/Services, src/Govern.Client/wwwroot/css/game-ui.css]
updated: 2026-10-01
---

# Plan: F11b board animations from game events

**Status:** done 2026-10-01, PR #37, together with [F11a](F11a-game-log.md), which delivers the filtered events to the client.

## Context
The board redraws straight into each new view, so an opponent's move, an attack or a lost unit is easy to miss: a unit simply appears somewhere else, or is gone. With F11a every snapshot carries the events the player may see, in order. This plan plays them on the board as short animations, so both your own commands and the opponent's turn read as actions rather than jumps.

## Scope
- In: units walking along their path, an attack lunge with a floating damage number, a destroyed unit fading out, a short pulse on a founded or captured city and a finished improvement or bridge; animations for your own commands and for live opponent events; skipping; `prefers-reduced-motion`.
- Out: camera following events (a log click in F11a already centres a tile); replaying events that arrived while the page was closed (the log lists them); sound; a speed setting; sprite art beyond today's icons.

## Affected areas
- `Client/Features/Games/Services/BoardAnimationService.cs` (new): a queue of animations built from new log entries (by `Sequence`, so each entry plays once), and the display overrides the board reads while one plays.
- `Client/Features/Games/Components/Board/BoardPieces.razor`: a unit is drawn as a `<g>` with a CSS `transform` (instead of `<use x y>`), so a position change can transition; it reads the display position and ghosts from the service.
- `Client/Features/Games/Components/Board/BoardEffects.razor` (new) and `Enums/BoardLayer.cs` (`Effects`, the top layer): damage numbers and pulses, CSS keyframes in `wwwroot/css/game-ui.css`.
- `Features/Games/Components/GameTable.razor`: feeds new entries to the service when a view arrives (command response or SSE).
- Reuse `HexLayout` for tile centres and the F11a events and `GameLogItem` DTOs.

## Steps
1. Client: `BoardAnimationService` diffs the received log against the last sequence it saw; the first view after opening the game only records that sequence (nothing replays on load).
2. Client: per event, an animation: `unit_moved` steps tile by tile (about 120 ms per tile); `unit_attacked` moves the attacker a third of the way and back, then floats both damage numbers; `unit_destroyed` keeps a ghost of the unit and fades it; `city_founded`, `city_captured`, `improvement_built`, `bridge_built` pulse the tile. Other events play nothing.
3. Client: the new view is applied at once (the side panel, buttons and previews use it); only unit positions and ghosts are overridden until their animation ends. Input stays live: a click or a newer view finishes every running animation at once.
4. Client: with `prefers-reduced-motion: reduce`, moves and lunges are skipped and effects show without motion for a moment.

## Tests
- Unit: none (client only).
- Integration: none (no server change).
- Client (bUnit): the queue plays each sequence once and nothing on first load; a move overrides the unit's position until it ends; a newer view or a click finishes the queue; reduced motion skips moves; a destroyed unit's ghost goes away; the effects layer renders the damage numbers.
- Architecture: none.

## Docs & rules
- `docs/features/game-ui.md`: board layers (Effects), animations section (what plays, timing, skipping, reduced motion), component catalog.
- No `.claude` rule change.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green. Manual with Playwright and two seats: own move and attack animate; the opponent's visible move and attack animate on the other seat; a click mid-animation finishes it; emulated reduced motion shows no movement. Screenshots in light, dark and 390 px.

## Risks & open questions
- **Rendering cost:** Blazor re-renders `BoardPieces` once per step. Only the moving unit's override changes, so the cost is one small diff per 120 ms; checked on a full board in the manual run.
- **Timing between seats:** animations are cosmetic and never delay a command or the view, so a slow animation can't desync anything.
