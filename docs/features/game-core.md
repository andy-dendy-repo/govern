---
title: Game Core engine
tags: [game, core, engine, hex, rules, ruleset, abilities, fog-of-war, view-version, previews, ids, commands, events, serialization, schema-version, determinism]
code: [src/Govern.Core, tests/Govern.UnitTests/Core]
updated: 2026-09-30
---

# Game Core engine

`Govern.Core` is the rules engine of Govern's 2-player, turn-based hex strategy game, a much-simplified Civilization. It is a pure library: the base class library only, no I/O, no clock, no `System.Random` and no project or package references. The Api uses it to validate every move authoritatively, and the Blazor client uses the same code to preview moves from its `PlayerView` ([view previews](#view-previews-for-the-client)). Why it is built this way: [ADR 0004](../decisions/0004-game-engine.md). Delivery status: [game delivery plan](../plans/done/game-delivery.md), [ruleset A](../plans/done/ruleset-a-core.md) (done; [B](../plans/deferred/ruleset-b-store-and-pin.md) deferred).

**Every number, list and text comes from the game's ruleset** (`Govern.Core.Rulesets.Ruleset`, the JSON document in `rulesets/`): unit and terrain stats, city rules, map sizes, spawn zones, starting units and names. Core holds the mechanics only. The format, defaults and validation are in [game data](../game/game-data.md); every entry point below takes the ruleset as its first parameter.

## Engine entry points (GameEngine)

| Member | Purpose |
|---|---|
| `Ruleset.Parse(json)` / `ParseValid(json)` | Reads, validates and hashes a ruleset document ([game data](../game/game-data.md#reading-a-ruleset-in-code)) |
| `GameFactory.Create(ruleset, GameSetup)` | New game: a generated map of the ruleset's size (or the setup's size, within the ruleset's limits) whose two halves mirror each other ([map generation](#map-generation)); each player gets the ruleset's starting units (by default a settler plus an adjacent soldier) on the default terrain. Turn 1, first player, version 0 |
| `GameEngine.Execute(ruleset, state, actor, command)` | Validates and applies one `GameCommand`. Returns `CommandResult`: a `GameUpdate` (new `GameState` + ordered `GameEvent`s) or a `RuleViolation`. A rejected command never changes the state |
| `GameEngine.ReachableTiles(ruleset, state, viewer, unitId)` | Tiles one of the viewer's units can move to this turn, for UI highlights. Another player's unit always returns an empty list |
| `GameEngine.ReachableTiles(ruleset, view, unitId)`, `AttackTargets(ruleset, view, unitId)`, `CombatForecast(ruleset, view, unitId, target)`, `CheckFoundCity(ruleset, view, unitId)` | What `Execute` would decide for a move, an attack (and the damage range of a fight) or founding a city, worked out from a `PlayerView` alone, for the client; nothing on the opponent's turn or in a finished game ([view previews](#view-previews-for-the-client)) |
| `GameEngine.ViewFor(ruleset, state, player)` | The fog-of-war `PlayerView`: the only shape a client may receive |
| `GameSerializer` | The one JSON format for storage (jsonb), the Api and the client |

After each accepted command, `Execute`:
1. runs the command's rule;
2. runs `VictoryRule` (eliminations, removal of departed players' units, win check);
3. updates every active player's explored tiles;
4. increments `GameState.Version` by one (an internal command count; clients never see it, see [View version](#view-version)).

## Hex map and coordinates (HexCoord)

- **Pointy-top hexes in axial coordinates:** `HexCoord(Q, R)`. `S = -Q - R` is derived.
- **Rectangular maps** use the **odd-r offset layout**: odd rows are shifted half a tile right. `HexCoord.FromOffset(column, row)` converts from offset coordinates, and `Column`/`Row` convert back.
- **Distance** is `(|ΔQ| + |ΔR| + |ΔS|) / 2`. `Neighbors()` always returns the six tiles in the same order (east, then counter-clockwise), which keeps pathfinding deterministic.
- **Map size:** `GameMap.Width × Height` (the ruleset's default, 20×14, within its limits, 8–40). Tiles are stored row by row. Terrain rules come from the ruleset (`Ruleset.Terrain`): movement cost, passable, sight bonus.
- **Mirror:** `GameMap.MirrorOf(coord)` is the tile opposite through the map's centre (column `W − 1 − c`, row `H − 1 − r`); on an even height it keeps neighbours neighbours.

## Terrain and map generation

| Terrain (default ruleset) | Movement cost | Passable | Sight bonus | Defence bonus |
|---|---|---|---|---|
| Plains (default terrain) | 1 | yes | 0 | 0 |
| Forest | 2 | yes | 0 | +3 |
| Hills | 2 | yes | +1 | +3 |
| Mountains | — | no | 0 | 0 |
| Lake | — | no | 0 | 0 |

- **Map generation** (`Map/MapGenerator`, internal, called by `GameFactory` after the spawns): patches of each `map.generation.patches` terrain grow from random seeds until they cover their share; every tile is written with its mirror, so both halves are the same. Spawn tiles and their mirrors stay default terrain; an impassable tile is placed only while all passable tiles stay connected (flood fill). The spawns themselves are not mirrored, so the map narrows the opponent's start only slightly (their starting tiles are plains). Every draw goes through `DeterministicRandom.NextHashed` (SHA-256 of the raw draw), because the whole map reaches the clients and raw `Next` results would expose the generator's low bits. Algorithm: [game logic](../game/game-logic.md#map-generation).
- **Sight bonus:** `VisibilityCalculator.VisibleTiles` adds the `SightBonus` of the tile a unit or city stands on. It only adds sight, so the fog invariants the previews rely on still hold.
- **Defence bonus:** added to a defending unit's strength in `CombatCalculator` (G9).
- **Deferred:** forests blocking sight (needs line of sight and breaks the "sees every reachable tile" invariant), terrain layers such as forested hills (G5).
- Reference: Red Blob Games, "Hexagonal Grids".

## Units and cities (the default ruleset)

| Unit | Class | Strength | Movement | Sight | Cost | Abilities |
|---|---|---|---|---|---|---|
| Settler | civilian | — | 2 | 2 | 6 | `found_city`: founds a city (consumed) |
| Soldier | melee | 20 | 2 | 2 | 4 | `attack`: fights adjacent enemy units; captures undefended enemy cities |
| Spearman | anti_cavalry | 18 | 2 | 2 | 4 | `attack` |
| Horseman | cavalry | 20 | 3 | 3 | 6 | `attack` |
| Archer | ranged | 10 (shoots at 22, range 2) | 2 | 2 | 5 | `ranged_attack`: shoots enemy units in range, no damage back |

- `UnitType` and `TerrainType` are ids from the ruleset (`readonly record struct`s written to JSON as the plain id). Stats come from `ruleset.Unit(type)`; rules check abilities (`UnitDefinition.CanFoundCity`/`CanAttack`/`CanRangedAttack`, `AttackRange`), never a unit id. A new unit is a ruleset entry only; a new ability also needs a `UnitAbility` value, a command and a rule.
- **Cities** (the ruleset's `cities` section; defaults in brackets):
  - sight (2);
  - production per turn at the end of the owner's turn (1);
  - minimum spacing from any other city (3);
  - initial production (Soldier);
  - named from a text template (`City {n}`);
  - a captured city's progress resets (yes).
- **Stacking:** at most one unit per tile. A unit may stand in its own city.

## Combat (G9)

- **State:** `Unit.Damage` (0 = unhurt; health = `combat.maxHealth` − damage). Stored as damage so games saved before G9 read as unhurt with no schema bump; the JSON leaves it out while it is 0. The view carries `UnitView.Health` for every unit the viewer sees.
- **`Engine/CombatCalculator`** (internal) works out a fight from two `Combatant`s (type, health, terrain): strengths with their `CombatModifier`s (health, counter, terrain), and each side's damage range. `AttackRule` rolls within those ranges; `GameEngine.CombatForecast` returns them to the client. Integer maths only.
- **Rolls** use `DeterministicRandom.NextHashed`, defender first, and only for a side whose range isn't a single value (a ranged attacker and a civilian take fixed damage).
- **Events:** `UnitAttackedEvent` (`unit_attacked`: both units, both positions, damage dealt and taken), then a `UnitDestroyedEvent` for each unit brought to 0.
- **Healing** is part of `EndTurnRule`, before production: a hurt unit with full movement left rested.
- Algorithm and numbers: [game logic](../game/game-logic.md#combat), [game data](../game/game-data.md#combat-combat).

## Game rules v1 (commands)

| Command (`type` in JSON) | Rule |
|---|---|
| `MoveUnitCommand` (`move_unit`) | Cheapest path (Dijkstra) to an empty tile within `MovementLeft`. Never through units or enemy cities. Emits `UnitMovedEvent` with the path |
| `FoundCityCommand` (`found_city`) | Settler with movement left, not on a city, spacing respected. The settler is removed and a city created (`CityFoundedEvent`) |
| `SetProductionCommand` (`set_production`) | Own city; changes what it builds and keeps its progress (`ProductionChangedEvent`) |
| `AttackCommand` (`attack`) | A unit with `attack` or `ranged_attack` and movement left; a target in range (adjacent for melee). Against a unit, both fight ([combat](#combat-g9)): `UnitAttackedEvent`, then `UnitDestroyedEvent` for each unit at 0 health; an undefended enemy city is captured by a melee attacker, which moves in (`CityCapturedEvent`). Movement drops to 0 |
| `EndTurnCommand` (`end_turn`) | The actor's cities produce. A finished unit spawns on the city or its first free neighbour with 0 movement; if the city is surrounded, it waits. The next active player gets full movement; the round (`Turn`) increases on wrap (`TurnEndedEvent`) |
| `ResignCommand` (`resign`) | Any active player, any time (`PlayerResignedEvent`) |

**Turn guard:** every command except Resign requires a running game and `actor == CurrentPlayer`. The unit or city must exist and belong to the actor.

**Elimination and victory:**
- A player with no cities and no settlers is eliminated (`PlayerEliminatedEvent`).
- Units of eliminated or resigned players are removed.
- When one active player is left, the game is `Finished`, `Winner` is set, and `GameWonEvent` is emitted.

## Rule violation error codes (RuleViolations)

Rule violations are returned as `RuleViolation(Code, Message)` (listed in `RuleViolations`), never thrown. The Api returns them as 409 with the same code and message ([games](games.md#sending-commands)). Codes are stable:

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
| `city.not_found` / `city.unknown_unit_type` | The actor has no such city / a unit type the game's ruleset doesn't have |
| `attack.not_adjacent` / `attack.out_of_range` / `attack.no_target` / `attack.own_target` | Attack target problems (`out_of_range` for ranged units; `no_target` also for a ranged unit aimed at an empty city) |

Exceptions are reserved for programming errors: an invalid `GameSetup`, an unknown command type, or `ViewFor` for a non-participant.

## Fog of war (PlayerView)

- A player sees every tile within the sight radius of their units and cities.
- Everything a player has ever seen accumulates in `Player.Explored`.
- `PlayerView.Tiles` marks each tile as `Unexplored` (terrain hidden), `Explored` (terrain known) or `Visible`. A `TileView` carries no coordinates: tiles are row-major (index `row * Width + column`), so `PlayerView.CoordAt(index)` gives a tile's position and `TileAt(coord)` the tile at a position. That keeps every snapshot about a third smaller.
- `PlayerView.Units` and `PlayerView.Cities` contain the viewer's own objects and enemy objects **on visible tiles only**.
- **Enemy production is hidden:** cities are `CityView`s, and an enemy city's `Producing` and `ProductionProgress` are null. Its progress would tell the viewer when the next enemy unit appears. A change in enemy production therefore never changes the viewer's `Version`.
- **Enemy movement is hidden:** units are `UnitView`s (`UnitView.From`), and a visible enemy unit's `MovementLeft` is null. It would show how far the unit moved through fog this turn, or that it attacked. A change in enemy movement alone therefore never changes the viewer's `Version`. `PlayerView.UnitAt` and `FindOwnUnit` return `UnitView`s; the view previews rebuild the viewer's own `Unit` (whose movement is always present) for the rules' shared checks.
- `PlayerView.Players` lists ids and statuses without the opponents' exploration.
- **Rejections leak nothing:** a hidden unit on a move target gives `move.unreachable`, not `move.occupied`. Another player's unit or city id gives `*.not_found`.
- **Invariants that keep rejections leak-free:** unit sight ≥ movement / cheapest step, and the sight of every unit with `found_city` ≥ city spacing − 1. `RulesetValidator` enforces both, so a ruleset that breaks them is rejected at load time until the rules filter by visibility.
- **Known residual leaks** (deferred, [game delivery plan](../plans/done/game-delivery.md#deferred-ideas)): a city's name (`City N`, counting the founder's cities at founding) shows how many cities its founder had then, hidden ones included.
- `PlayerId` values are chosen by the host. Govern uses the per-game seat id (`GamePlayer.Id`), never a user id, so the view reveals no account ids ([games](games.md)).
- Hosts must send clients `PlayerView`, never `GameState`.
- Nothing in a view counts the opponent's activity: `PlayerView.Version` is a fingerprint of the view itself, unit and city ids are hashed random draws (see below), and `Units`/`Cities` are ordered by id rather than creation. Raw `GameEvent`s describe the full state and must be filtered by visibility before being sent to a player.

## View previews for the client

The client only holds a `PlayerView`, never the `GameState`, so the engine answers "what may I do now?" from a view:

| Member | Answer | Equal to |
|---|---|---|
| `GameEngine.ReachableTiles(view, unitId)` | Tiles the unit can move to, row then column | the tiles a `MoveUnitCommand` would be accepted for |
| `GameEngine.AttackTargets(view, unitId)` | Tiles in range with an enemy unit (or, for melee, an enemy city) | the tiles an `AttackCommand` would be accepted for |
| `GameEngine.CombatForecast(view, unitId, target)` | Both strengths, their modifiers and each side's damage range; `null` for a capture or a rejected attack | the range `Execute` rolls in |
| `GameEngine.CheckFoundCity(view, unitId)` | `null`, or the `RuleViolation` | what a `FoundCityCommand` would return |

- All three give nothing (or the violation) when the game is over, it isn't the viewer's turn, or the unit isn't the viewer's, the same as the command guard.
- **Why a view is enough:** unit sight ≥ movement, founder sight ≥ city spacing − 1, and ranged range ≤ sight (enforced by `RulesetValidator`). Every tile a unit could reach, attack or be blocked by is therefore visible, and the view shows every unit and city on it. Reachability over a view enters only visible tiles.
- **They can't drift from the rules:** `AttackRule.Check` and `FoundCityRule.Check` are shared by the rule and the preview, as are the turn check (`CommandGuardRule.CheckTurn`) and the Dijkstra in `ReachabilityCalculator`. `GameEnginePreviewTests` compares each preview with `Execute` for every tile within 3 of the unit, over scenarios covering own and enemy blockers, an own city in reach, enemies just outside sight (where the view and the state differ), the map edge, no movement, the opponent's turn, an enemy unit id, a finished game, and terrain: forest around the unit (with full and with 1 movement left), mountains and lake around it, a path around a lake, a unit on hills that sees further, an archer with targets in and out of range, and a horseman. `CombatForecastTests` checks over 1000 seeds that `Execute` rolls exactly the forecast's lowest and highest damage for each kind of fight. A new terrain type with a new cost or blocker adds its own parity scenarios.

## View version

`PlayerView.Version` is an opaque fingerprint of one player's view: the first 8 bytes of the SHA-256 of the view's `GameSerializer` JSON (with `version` 0), masked to a non-negative `long`.

- **Equal views have equal versions**, and anything hidden from the viewer can't change the value. A command count would tell the opponent how often the other player acted; the fingerprint doesn't.
- **Compare it for equality only.** It is neither ordered nor sequential. Clients echo it as `expectedVersion`, and the host rejects a command when the caller's current view has a different version ([games](games.md#sending-commands)).
- Each player has their own version for the same state, since their views differ.
- `GameState.Version` still counts accepted commands; it stays inside the state.
- The fingerprint depends on the exact `GameSerializer` bytes, so a serializer change (e.g. a .NET upgrade that escapes differently) changes every version once: each open client gets one `game.stale_version` and reloads.

## Determinism and randomness

- The same state, actor and command always produce the same result.
- Randomness (spawn rows, map generation, combat damage) comes from `DeterministicRandom` (SplitMix64). Its state is stored in `GameState.RngState`, so replaying a command log rebuilds the exact game on any .NET version.
- Pathfinding breaks cost ties by row, then column, so event paths never depend on the `PriorityQueue` implementation.
- The host must generate `GameSetup.Seed` with a cryptographically secure RNG and never send it to clients, because spawn positions follow from it.
- **Ids:** unit and city ids come from the same generator (`GameState.AllocateId`), including the four starting units. Each id is 31 bits of the **SHA-256 of a 64-bit draw**, redrawn when 0 or already used by a unit or city. Sequential ids would reveal how many units and cities the opponent created, and raw draws would be worse: SplitMix64 is invertible, so two raw ids let a player rebuild `RngState` in seconds and with it the opponent's spawn and every future id. Hashed ids reveal nothing and still replay identically.
- **Never send raw `DeterministicRandom` output to a client.** Hash anything a player can see (ids), or keep it to a few bits of a small range (spawn tiles). Guessing the state from hashed ids takes about 2⁶⁴ work, the same as guessing the 64-bit seed.

## JSON format and schema version (GameSerializer)

- `GameSerializer.Options` uses web defaults (camelCase), with enums as camelCase strings (numbers are rejected). `UnitType` and `TerrainType` are written as their plain ruleset ids (`"settler"`), the same JSON the former enums gave, so stored games read unchanged.
- Commands and events are polymorphic, with a `type` discriminator that may appear anywhere in the object.
- The discriminator names are part of the storage and wire format. Never rename them.
- **Required values:** every constructor value is required (`RespectRequiredConstructorParameters`). Bad client input, including a missing `type`, always surfaces as `JsonException` (a 400 in the Api), never a 500.
- **Schema version:** `GameState.SchemaVersion` (currently 2) is read by `GameSerializer.DeserializeState` **before** binding. Older known versions are upgraded on read, and a missing or unknown version throws `NotSupportedException`. Bump it and add the upgrade on breaking changes.
  - **Version 1 → 2:** version 1 had a `nextId` counter. The upgrade drops it and keeps the existing ids; new ids come from the generator.

## Tests

`tests/Govern.UnitTests/Core` covers:
- hex math;
- map bounds;
- the RNG;
- the ruleset: the default file parses, and one case per validation rule (`Rulesets/RulesetTests`);
- game setup, including a golden test that the default ruleset gives exactly the games the former constants gave (`State/GameFactoryTests.Create_KnownSeed_GivesTheRecordedState`);
- rules reading changed ruleset values (costs, production, spacing, names, abilities, capture);
- every rule's happy path and errors;
- fog of war, including hidden enemy production and the tile index ↔ coordinate mapping;
- the view previews' parity with `Execute` (`Engine/GameEnginePreviewTests`);
- JSON round-trips, including a view without tile coordinates;
- a replay determinism check.

`Builders/GameStateBuilder` places units and cities on a 12×10 map for scenario tests. `Builders/TestRulesets` loads the real `rulesets/default/v2.json` (copied next to the tests) and builds variants of it (`TestRulesets.With(json => …)`); `GameStateBuilder.WithTerrain` puts forest, hills, mountains or lake on chosen tiles.

Architecture tests keep Core pure:
- `LayerDependencyTests.Core_DependsOnlyOnBaseClassLibrary`
- `LayerDependencyTests.Core_DoesNotUseNondeterministicOrIoApis`: no `Random`, clock, `System.IO`/`Net`/`Threading.Tasks`
- `LayerDependencyTests.Core_DoesNotCreateIdentifiersOrUseHashCodes`: no `Guid.NewGuid`/`CreateVersion7`/`GetHashCode(`
- `ProjectReferenceTests.Core_HasNoPackageReferences`
- `FolderStructureTests`: the Core folder whitelist, and each `Actions/XxxAction.cs` holding exactly `XxxCommand` + `XxxRule`
