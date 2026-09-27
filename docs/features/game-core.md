---
title: Game Core engine
tags: [game, core, engine, hex, rules, fog-of-war, commands, events, serialization, determinism]
code: [src/Govern.Core, tests/Govern.UnitTests/Core]
updated: 2026-09-27
---

# Game Core engine

`Govern.Core` is the rules engine of Govern's 2-player, turn-based hex strategy game, a much-simplified Civilization. It is a pure library: the base class library only, no I/O, no clock, no `System.Random` and no project or package references. The Api uses it to validate every move authoritatively, and the Blazor client will use the same code to preview moves. Why it is built this way: [ADR 0004](../decisions/0004-game-engine.md). Delivery status: [game delivery plan](../plans/game-delivery.md).

## Engine entry points (GameEngine)

| Member | Purpose |
|---|---|
| `GameFactory.Create(GameSetup)` | New game: plains map, each player gets a settler plus an adjacent soldier. Turn 1, first player, version 0 |
| `GameEngine.Execute(state, actor, command)` | Validates and applies one `GameCommand`. Returns `CommandResult`: a `GameUpdate` (new `GameState` + ordered `GameEvent`s) or a `GameError`. A rejected command never changes the state |
| `GameEngine.ReachableTiles(state, viewer, unitId)` | Tiles one of the viewer's units can move to this turn, for UI highlights. Another player's unit always returns an empty list |
| `GameEngine.ViewFor(state, player)` | The fog-of-war `PlayerView`: the only shape a client may receive |
| `GameSerializer` | The one JSON format for storage (jsonb), the Api and the client |

After each accepted command, `Execute`:
1. runs the command's rule;
2. runs `VictoryRule` (eliminations, removal of departed players' units, win check);
3. updates every active player's explored tiles;
4. increments `GameState.Version` by one.

## Hex map and coordinates (HexCoord)

- **Pointy-top hexes in axial coordinates:** `HexCoord(Q, R)`. `S = -Q - R` is derived.
- **Rectangular maps** use the **odd-r offset layout**: odd rows are shifted half a tile right. `HexCoord.FromOffset(column, row)` converts from offset coordinates, and `Column`/`Row` convert back.
- **Distance** is `(|ΔQ| + |ΔR| + |ΔS|) / 2`. `Neighbors()` always returns the six tiles in the same order (east, then counter-clockwise), which keeps pathfinding deterministic.
- **Map size:** `GameMap.Width × Height` (default 20×14, allowed 8–40). Tiles are stored row by row. Every tile is `Plains` (movement cost 1) for now; terrain rules live in `TerrainCatalog`.
- Reference: Red Blob Games, "Hexagonal Grids".

## Units and cities (catalogs)

| Unit | Movement | Sight | Cost | Abilities |
|---|---|---|---|---|
| Settler | 2 | 2 | 6 | Founds a city (consumed) |
| Soldier | 2 | 2 | 4 | Attacks adjacent enemy units; captures undefended enemy cities |

- Unit stats are data in `UnitCatalog`. A new unit is a `UnitType` value plus a catalog entry; a new ability also needs a command and a rule.
- **Cities:**
  - sight 2;
  - +1 production at the end of the owner's turn;
  - at least 3 tiles from any other city (`City.MinimumSpacing`);
  - build a Soldier by default;
  - auto-named `City N`.
- **Stacking:** at most one unit per tile. A unit may stand in its own city.

## Game rules v1 (commands)

| Command (`type` in JSON) | Rule |
|---|---|
| `MoveUnitCommand` (`move_unit`) | Cheapest path (Dijkstra) to an empty tile within `MovementLeft`. Never through units or enemy cities. Emits `UnitMovedEvent` with the path |
| `FoundCityCommand` (`found_city`) | Settler with movement left, not on a city, spacing respected. The settler is removed and a city created (`CityFoundedEvent`) |
| `SetProductionCommand` (`set_production`) | Own city; changes what it builds and keeps its progress (`ProductionChangedEvent`) |
| `AttackCommand` (`attack`) | Soldier with movement left, adjacent target. The attacker always wins in v1: a defending unit is destroyed (`UnitDestroyedEvent`); an undefended enemy city is captured and the soldier moves in (`CityCapturedEvent`). Movement drops to 0 |
| `EndTurnCommand` (`end_turn`) | The actor's cities produce. A finished unit spawns on the city or its first free neighbour with 0 movement; if the city is surrounded, it waits. The next active player gets full movement; the round (`Turn`) increases on wrap (`TurnEndedEvent`) |
| `ResignCommand` (`resign`) | Any active player, any time (`PlayerResignedEvent`) |

**Turn guard:** every command except Resign requires a running game and `actor == CurrentPlayer`. The unit or city must exist and belong to the actor.

**Elimination and victory:**
- A player with no cities and no settlers is eliminated (`PlayerEliminatedEvent`).
- Units of eliminated or resigned players are removed.
- When one active player is left, the game is `Finished`, `Winner` is set, and `GameWonEvent` is emitted.

## Rule violation error codes (GameErrors)

Rule violations are returned as `GameError(Code, Message)`, never thrown. Codes are stable:

| Code | When |
|---|---|
| `game.finished` | The game is over |
| `game.not_a_player` | The actor is not an active participant |
| `game.not_your_turn` | Another player's turn |
| `unit.not_found` | The actor has no such unit (missing and "someone else's" are deliberately the same) |
| `unit.no_movement` | No movement left this turn |
| `unit.cannot_found_city` / `unit.cannot_attack` | Wrong unit type for the command |
| `move.occupied` / `move.unreachable` | A **visible** unit stands on the target / off map, own tile, blocked, too far, or a hidden unit is there |
| `city.tile_taken` / `city.too_close` | Founding on a city / closer than the minimum spacing |
| `city.not_found` / `city.unknown_unit_type` | The actor has no such city / undefined unit type |
| `attack.not_adjacent` / `attack.no_target` / `attack.own_target` | Attack target problems |

Exceptions are reserved for programming errors: an invalid `GameSetup`, an unknown command type, or `ViewFor` for a non-participant.

## Fog of war (PlayerView)

- A player sees every tile within the sight radius of their units and cities.
- Everything a player has ever seen accumulates in `Player.Explored`.
- `PlayerView.Tiles` marks each tile as `Unexplored` (terrain hidden), `Explored` (terrain known) or `Visible`.
- `PlayerView.Units` and `PlayerView.Cities` contain the viewer's own objects and enemy objects **on visible tiles only**.
- `PlayerView.Players` lists ids and statuses without the opponents' exploration.
- **Rejections leak nothing:** a hidden unit on a move target gives `move.unreachable`, not `move.occupied`. Another player's unit or city id gives `*.not_found`. City names count only the founder's cities.
- **Invariants that keep rejections leak-free:** unit sight ≥ movement / cheapest step, and settler sight ≥ `City.MinimumSpacing - 1`. `UnitCatalogTests` enforces both, so changing a stat that breaks them fails the build until the rules filter by visibility.
- Hosts must send clients `PlayerView`, never `GameState`. Raw `GameEvent`s describe the full state and must be filtered by visibility before being sent to a player.

## Determinism and randomness

- The same state, actor and command always produce the same result.
- Randomness (spawn rows today, combat later) comes from `DeterministicRandom` (SplitMix64). Its state is stored in `GameState.RngState`, so replaying a command log rebuilds the exact game on any .NET version.
- Pathfinding breaks cost ties by row, then column, so event paths never depend on the `PriorityQueue` implementation.
- The host must generate `GameSetup.Seed` with a cryptographically secure RNG and never send it to clients, because spawn positions follow from it.
- Ids for units and cities come from the `GameState.NextId` counter.

## JSON format and schema version (GameSerializer)

- `GameSerializer.Options` uses web defaults (camelCase), with enums as camelCase strings (numbers are rejected).
- Commands and events are polymorphic, with a `type` discriminator that may appear anywhere in the object.
- The discriminator names are part of the storage and wire format. Never rename them.
- **Required values:** every constructor value is required (`RespectRequiredConstructorParameters`). Bad client input, including a missing `type`, always surfaces as `JsonException` (a 400 in the Api), never a 500.
- **Schema version:** `GameState.SchemaVersion` (currently 1) is read by `GameSerializer.DeserializeState` **before** binding; a missing or different version throws `NotSupportedException`. Bump it and migrate stored games on breaking changes.

## Tests

`tests/Govern.UnitTests/Core` covers:
- hex math;
- map bounds;
- the RNG;
- game setup;
- every rule's happy path and errors;
- fog of war;
- JSON round-trips;
- a replay determinism check.

`Builders/GameStateBuilder` places units and cities on a 12×10 map for scenario tests.

Architecture tests keep Core pure:
- `LayerDependencyTests.Core_DependsOnlyOnBaseClassLibrary`
- `LayerDependencyTests.Core_DoesNotUseNondeterministicOrIoApis`: no `Random`, clock, `System.IO`/`Net`/`Threading.Tasks`
- `LayerDependencyTests.Core_DoesNotCreateIdentifiersOrUseHashCodes`: no `Guid.NewGuid`/`CreateVersion7`/`GetHashCode(`
- `ProjectReferenceTests.Core_HasNoPackageReferences`
- `FolderStructureTests`: the Core folder whitelist, and each `Actions/XxxAction.cs` holding exactly `XxxCommand` + `XxxRule`
