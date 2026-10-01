---
title: "Plan: F11a game log (engine events filtered by visibility)"
tags: [plan, game, core, events, game-log, fog-of-war, visibility, sse, live-updates, client, side-panel]
code: [src/Govern.Core/Events, src/Govern.Core/Visibility, src/Govern.Domain/Entities/Game.cs, src/Govern.Application/Games, src/Govern.Client/Features/Games]
updated: 2026-10-01
---

# Plan: F11a game log (engine events filtered by visibility)

**Status:** planned 2026-10-01 (backlog F11a, Next). F11b [animations](F11b-animations.md) builds on the events this plan delivers.

## Context
Every accepted command already produces engine events (`GameUpdate.Events`, 19 kinds after G5–G10), but the server drops them: only `VictoryRule` reads them, and players only see the board jump to its new state. Above all, a player doesn't learn what the opponent did during their turn ("their Archer hit your Soldier for 22"). Events describe the full state, so [ADR 0005](../../decisions/0005-live-updates.md) kept them server-side until a log needed them, together with a `VisibilityCalculator.EventsFor` that doesn't exist yet. This plan adds that filter, stores each player's filtered events, sends them with the game snapshot, and shows them as a log in the side panel.

## Scope
- In: a per-player event filter in Core (with slimmer event shapes that are safe to send), per-seat log storage, the recent log in `GetGameResponse` (and so in every SSE snapshot), a log list in the side panel with one sentence per event.
- Out: animations (F11b); paging back through older turns (stored, not served, until someone asks to scroll further); localisation of the log text (design roadmap question 3); counting or hinting at hidden opponent actions (that would leak, see Risks).

## Affected areas
- `Core/Events/*.cs`: `CityFoundedEvent` and `UnitCreatedEvent` carry whole `City`/`Unit` records today (stock, production, job). They change to public fields only.
- `Core/Visibility/VisibilityCalculator.cs`: new `EventsFor`. Reuses `VisibleTiles`.
- `Domain/Entities/GameLogEntry.cs` (new child of `Game`), `Domain/Entities/Game.cs` (`Execute` writes the entries).
- `Infrastructure/Persistence/Configurations/GameLogEntryConfiguration.cs`, migration `AddGameLog`.
- `Application/Games/Contracts/GetGameContract.cs` (`GetGameResponse.Log`), `SendGameCommandContract.cs` and `WatchGameContract.cs` (load the window). `Application/Games/Models/GameLogItem.cs`.
- Client: `Features/Games/Components/GameLog.razor`, `Features/Games/Services/GameLogFormatter.cs`, `Screen/SidePanel.razor`, `Models` DTO for the response.

## Steps
1. **Core: safe event shapes.** `CityFoundedEvent(CityId, Owner, Position, Name, SettlerId)` and `UnitCreatedEvent(UnitId, Owner, Type, Position, CityId)`. No other event carries private state; the per-event table below decides who sees each one. Events have never been stored or sent, so no JSON compatibility work.
2. **Core: `VisibilityCalculator.EventsFor(ruleset, before, update, viewer)`** returns the events this player may see, in order. "Seen tiles" = the viewer's visible tiles in `before` ∪ in `update.State` (what they could see when the command started or after it ended).

   | Event | Actor sees | Opponent sees |
   |---|---|---|
   | `turn_ended`, `player_resigned`, `player_eliminated`, `game_won` | yes | yes |
   | `unit_moved` | yes | the path cut to seen tiles; dropped when none |
   | `unit_attacked`, `unit_destroyed` | yes | when either tile is seen, or the unit is theirs |
   | `city_captured` | yes | when the city's tile is seen or the city was theirs |
   | `city_founded`, `unit_created`, `improvement_built`, `bridge_built`, `territory_grew` | yes | when the tile (either bridge end) is seen |
   | `production_changed`, `build_started`, `building_completed`, `upkeep_priority_set`, `unit_starved`, `building_damaged` | yes (owner) | never (the view already shows what the opponent may know, such as a unit's health) |

   "Actor" means the owner of the thing, so end-of-turn events of the player ending their turn follow the same table.
3. **Domain: `GameLogEntry`** (Id, GameId, `Seat` = the player's `PlayerId`, `Sequence` per seat, `Turn`, `Events` as jsonb `ImmutableArray<GameEvent>`). `Game.Execute` calls `EventsFor` for each seat after an accepted command and adds an entry when the list isn't empty. The entry has no game version or timestamp: the state version counts every command, so it would count the opponent's hidden ones (the same rule as `PlayerView.Version`).
4. **Infrastructure:** configuration (index on `(GameId, Seat, Sequence)`, cascade delete with the game), migration `AddGameLog`. Games already running start with an empty log.
5. **Application:** `GetGameResponse` gains `IReadOnlyList<GameLogItem> Log`: the caller's entries from the previous turn on (the opponent's last turn and your own), oldest first. `GameLogItem(long Sequence, int Turn, ImmutableArray<GameEvent> Events)`. `GetGame`, `SendGameCommand` and `WatchGame` load the window with one indexed query. The SSE loop still pushes only when the view changes; a seen event always changes the view, so no event is missed, and a coalesced push carries every entry in the window.
6. **Api:** none (the endpoints return `GetGameResponse` already). `Govern.Api.http` sample unchanged.
7. **Client:** `GameLogFormatter` turns an event into one sentence with ruleset names and the seat colour ("Your Archer hit Seat 2's Soldier: 22 damage, 0 taken", "Seat 2 founded a city", "Turn 14: your turn"). `GameLog` lists the window newest first, grouped by turn; a click on an entry with a tile selects and centres it. `SidePanel` shows it below the selection panel, collapsible, open by default.

## Tests
- Unit (Core): `EventsFor` per row of the table, both seats: own events unfiltered; hidden opponent events dropped; a path entering and leaving sight is cut to the seen tiles; an attack on your unit from the fog is seen; a captured city of yours is seen; private events never reach the opponent; the filtered events carry no stock, production or job (serialised JSON check).
- Unit (Domain): `Game.Execute` writes one entry per seat with visible events, sequences count per seat, nothing for a rejected command.
- Integration: a command returns the actor's new entry; the opponent's `GET /api/games/{id}` and watch stream show the filtered entry; a hidden move adds nothing for the opponent; 401 and 404 already covered by the game endpoints.
- Client (bUnit): formatter sentence per event type; `GameLog` groups by turn, newest first, a click selects the tile.
- Architecture: none (existing rules cover a Domain entity and Core types).

## Docs & rules
- `docs/features/game-core.md`: events section (safe shapes, `EventsFor` and the table).
- `docs/game/game-logic.md#fog-of-war`: what a player's log shows. `docs/game/rules.md`: one line on the log only showing what you could see.
- `docs/features/games.md`: log storage and `GetGameResponse.Log`. `docs/features/game-ui.md`: the log in the side panel zone and component catalog.
- `docs/decisions/0005-live-updates.md`: snapshots now carry the filtered recent log.
- No `.claude` rule change.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green. Manual: two browsers, one per seat; move a unit in and out of the other's sight, attack, found a city, end turn; each log shows only what that seat could see.

## Risks & open questions
- **Leaks are the main risk.** The filter must never add information the two views don't already give; the unit tests check each event type for both seats. Dropping hidden events rather than counting them is deliberate.
- **Open (owner):** where the log lives. Default in this plan: a collapsible list at the bottom of the side panel. The alternative is a Log button in the top bar that opens it as a popup.
