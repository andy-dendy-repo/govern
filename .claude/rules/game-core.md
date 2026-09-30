---
paths:
  - "src/Govern.Core/**"
  - "tests/Govern.UnitTests/Core/**"
---

# Game Core (Govern.Core)

`Govern.Core` is the pure, deterministic game engine. The server uses it to validate moves authoritatively, and the Blazor client uses it to preview them from a `PlayerView`. Behaviour and rules are in `docs/features/game-core.md`; the reasoning is in `docs/decisions/0004-game-engine.md`.

## Purity (enforced)

- **Base class library only:** no project references, no `PackageReference` (`ProjectReferenceTests.Core_HasNoPackageReferences`) and no `Microsoft.*` types (`LayerDependencyTests.Core_DependsOnlyOnBaseClassLibrary`). Core does not use `Govern.Utils`: it has its own `RuleViolation`/`CommandResult`, and Application maps `RuleViolation` to `Error`. (`RuleViolation`, not `GameError`, so the name never clashes with the Application feature's `GameErrors` or with `Utils.Results.Error`.)
- **No I/O, clock, async or `System.Random`** (`Core_DoesNotUseNondeterministicOrIoApis`). **No `Guid.NewGuid`, `Guid.CreateVersion7` or `GetHashCode(`** (`Core_DoesNotCreateIdentifiersOrUseHashCodes`). Logging and DI are also out (no `Microsoft.*`). Randomness goes through `DeterministicRandom`, whose state lives in `GameState.RngState`; new unit and city ids come from the same generator (`GameState.AllocateId`), hashed with SHA-256. **Never send raw `DeterministicRandom` output to a client** (SplitMix64 is invertible: two raw values reveal `RngState`); hash it (`DeterministicRandom.NextHashed`), or expose only a few bits of a small range. Never add a counter an opponent could read activity from. `GameFactoryTests.Create_Ids_AreNotRawGeneratorDraws` guards the ids. The same state, actor and command must always give the same result; order-dependent code sorts explicitly.

## State and rules

- `GameState` and everything in it are immutable records. Change them with `with` and the `internal` `GameState.WithUnit`/`WithoutUnit`/`WithCity`/`WithPlayer`/`AllocateId` helpers. Collections are `ImmutableArray` so the JSON stays plain.
- **Only `GameEngine.Execute` changes a game.** Adding an action means:
  1. one `Actions/XxxAction.cs` holding the `XxxCommand` record and the `internal static class XxxRule` with `Apply(ruleset, state, actor, command) → CommandResult`;
  2. its `[JsonDerivedType]` on `Engine/GameCommand`;
  3. a case in `GameEngine.Dispatch`;
  4. new events in `Events/XxxEvent` plus `[JsonDerivedType]` on `GameEvent`, and new codes in `Engine/RuleViolations`.
- **Rule violations are returned as `RuleViolations.Xxx`** (a stable snake_case code) and never thrown. Throw only for programming errors: an invalid `GameSetup`, an unknown command type, a missing entity inside a rule.
- **Every game number, list and text comes from the ruleset** (`Rulesets/Ruleset`, parsed from `rulesets/<key>/vN.json`), never a constant in Core. Every engine entry point and rule takes the game's `Ruleset` as its first parameter. A new stat is a field in the JSON, its record in `Rulesets/` (or `UnitDefinition`/`TerrainDefinition`), a `RulesetValidator` check with a `RulesetTests` case, and the docs in `docs/game/game-data.md`. Any rules change (a new or changed stat, unit, terrain or mechanic) also updates the player rules reference `docs/game/rules.md` in the same change; `RulesReferenceTests` (`tests/Govern.ArchitectureTests`) checks its "Terrain types", "Unit types", "Class counters" and "Game numbers" tables against the default ruleset, and a new ability needs its label in that test. Rules check **abilities** (`UnitDefinition.CanFoundCity`/`CanAttack`/`CanRangedAttack`, `AttackRange`, from `UnitAbility`) and **classes** (`UnitDefinition.Class`, for `combat.counters`), never a unit id; a new ability is a `UnitAbility` value plus its rule.
- **`UnitType`/`TerrainType` are ruleset ids** (`readonly record struct`s written to JSON as the plain id). There are no `UnitType.Settler`-style constants; tests use `Builders/TestRulesets` (the real default file, and variants via `TestRulesets.With`).
- **Clients only ever receive `GameEngine.ViewFor`** (`PlayerView`), never `GameState` or unfiltered events. Anything added to state that an opponent must not see must be filtered in `VisibilityCalculator.ViewFor`.
- **`PlayerView.Version` is an opaque fingerprint** of the rest of the view (SHA-256 of its JSON). Everything in the view is hashed, so a new view field must hold only what the viewer may see; never put `GameState.Version` or another counter into the view.
- **Rejections must not leak hidden information.** Another player's unit or city is `*.not_found`. A rule that inspects tiles or objects the actor cannot see must return the same error it would return if they were empty. Keep the fog invariants that `RulesetValidator` enforces (sight ≥ movement ÷ cheapest passable cost; `found_city` units' sight ≥ city spacing − 1) or filter by visibility.
- **View shape:** `TileView` has no coordinates (tiles are row-major; use `PlayerView.CoordAt`/`TileAt`), cities in a view are `CityView`s built by `CityView.From`, with an enemy city's production null, and units are `UnitView`s built by `UnitView.From`, with an enemy unit's movement null and every seen unit's health. Never add per-tile coordinates, an opponent's production or an opponent's movement back. Any new per-object field in the view needs the same decision: everyone, owner only, or visible tiles only.
- **View previews** (`GameEngine.ReachableTiles`/`AttackTargets`/`CombatForecast`/`CheckFoundCity` over a `PlayerView`, in `Visibility/ViewPreviewCalculator`) must give exactly what `Execute` gives. They share the rules' checks (`CommandGuardRule.CheckTurn`, `AttackRule.Check`, `FoundCityRule.Check`, `ReachabilityCalculator`, `CombatCalculator`) instead of copying them. A new action the client should preview gets a shared check and a case in `Engine/GameEnginePreviewTests` (parity with `Execute` for every tile near the unit). The previews are exact only while the validator's sight invariants hold (a terrain `sightBonus` only adds sight, so it keeps them; anything that *reduces* sight, such as forests blocking it, breaks them and needs a design change first). The parity scenarios cover forest (cost 2), mountains and lake (impassable), hills (sight bonus) and a range-2 archer; the validator keeps a ranged unit's range within its sight; a new terrain type with a new kind of cost or blocker adds parity scenarios with it (`GameStateBuilder.WithTerrain`).
- **Combat** (`Engine/CombatCalculator`, internal) is integer maths only, so the browser's forecast equals the server's roll range. `AttackRule` rolls within `CombatCalculator`'s ranges with `DeterministicRandom.NextHashed` (damage is shown to both players); `CombatForecastTests` checks over many seeds that `Execute` hits exactly the forecast's lowest and highest damage for each kind of fight, so a new modifier adds a fight there. Health is stored as `Unit.Damage` (0 = unhurt, left out of the JSON while 0), so older games read unchanged; health = `combat.maxHealth` − damage.
- **Map generation** is `Map/MapGenerator` (internal, called by `GameFactory`). It draws from the game's `DeterministicRandom` after the spawns and writes every tile together with its mirror (`GameMap.MirrorOf`), so both halves are the same; spawns are not mirrored (that would tell each player where the opponent started). Its draws use `DeterministicRandom.NextHashed`: anything a client sees in bulk must never come from raw `Next`. Starting tiles stay default terrain and impassable terrain never splits the passable tiles. Its parameters are the ruleset's `map.generation.patches`; tested through `GameFactory.Create` in `Core/Map/MapGeneratorTests`. Changing the generator changes every new game: re-record `GameFactoryTests.Create_KnownSeed_GivesTheRecordedState`.
- **The client** (`Govern.Client`) references Core for view types, command records and the view previews only; it never gets `GameState`, `Execute` or `GameSerializer` (`LayerDependencyTests.Client_UsesCoreOnlyForViewsAndPreviews`). Anything public added to Core may be used by the browser, so keep it pure and cheap.
- **Ids and enum values from commands are untrusted:** look a unit type up with `ruleset.FindUnit` (null → `RuleViolations.UnknownUnitType`) and check `Enum.IsDefined` for enums before using them; `ruleset.Unit`/`Terrain` throw on an unknown id, which the Api would return as 500.

## Serialization

- JSON goes through `GameSerializer.Options` only.
- `type` discriminators (`move_unit`, `unit_moved`, …) are a storage and wire format: never rename one.
- Computed properties get `[JsonIgnore]`.
- A breaking change to the shape of `GameState` bumps `GameState.CurrentSchemaVersion` and needs an upgrade in `GameSerializer.DeserializeState` for stored games (e.g. v1 → v2 drops `nextId`).

## Folders and naming

Whitelisted by `Templates.Core()` and checked by `FolderStructureTests`:

| Folder | Holds |
|---|---|
| `Engine/` | The pipeline: `GameEngine`, `CommandResult`, `GameUpdate`, `GameCommand` (base + discriminators), `RuleViolation`/`RuleViolations`, the cross-action `CommandGuardRule` and `VictoryRule`, `DeterministicRandom` |
| `State/` | The snapshot: `GameState`, `GameStatus`, `GameSetup`, `GameFactory`, `Player`, `PlayerId`, `PlayerStatus` |
| `Map/` | Geometry and movement: `HexCoord`, `GameMap`, `Tile`, `TerrainType`/`TerrainDefinition`, `ReachabilityCalculator` |
| `Units/`, `Cities/` | The things on the map, `UnitType`/`UnitDefinition`/`UnitAbility` |
| `Actions/` | Flat; one `XxxAction.cs` per player action with exactly `XxxCommand` + `XxxRule` (`ActionFiles_DeclareExactlyThePrefixedCommandAndRule`) |
| `Events/` | `GameEvent` + events; shared by several actions, so not per action. Types end with `Event` |
| `Visibility/` | Fog of war: `VisibilityCalculator`, `PlayerView` and its parts (`TileView`, `UnitView`, `CityView`, `PlayerSummary`), and the view-based previews (`ViewPreviewCalculator`) |
| `Serialization/` | `GameSerializer` |
| `Rulesets/` | `Ruleset` (the parsed document and its lookups), its section records (`SetupRules`, `MapRules`, `SpawnRules`, `CityRules`), `RulesetParser` (strict JSON), `RulesetValidator`, `RulesetProblem`/`RulesetParseResult` |

- There are no root files.
- Concept folders may get sub-folders when they grow.
- A new top-level folder needs a template change: update `Templates.CoreFolders`, this table and `architecture.md` together.

## Tests

- **Location:** `tests/Govern.UnitTests/Core/<Folder>/XxxTests.cs`.
- **Rules:** tested through `GameEngine.Execute`, with scenarios from `Builders/GameStateBuilder`. Give both players a city or settler (e.g. `WithCapitals()`), otherwise `VictoryRule` ends the game after the first command.
- **Assertions:** `result.ShouldSucceed()` / `result.ShouldFailWith(RuleViolations.X)`.
- **Records holding `ImmutableArray` don't compare structurally.** Compare their elements, or the `GameSerializer` JSON.
