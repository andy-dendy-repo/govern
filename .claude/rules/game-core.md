---
paths:
  - "src/Govern.Core/**"
  - "tests/Govern.UnitTests/Core/**"
---

# Game Core (Govern.Core)

`Govern.Core` is the pure, deterministic game engine. The server uses it to validate moves authoritatively, and the Blazor client will use it to preview them. Behaviour and rules are in `docs/features/game-core.md`; the reasoning is in `docs/decisions/0004-game-engine.md`.

## Purity (enforced)

- **Base class library only:** no project references, no `PackageReference` (`ProjectReferenceTests.Core_HasNoPackageReferences`) and no `Microsoft.*` types (`LayerDependencyTests.Core_DependsOnlyOnBaseClassLibrary`). Core does not use `Govern.Utils`: it has its own `GameError`/`CommandResult`, and Application maps `GameError` to `Error`.
- **No I/O, clock, async or `System.Random`** (`Core_DoesNotUseNondeterministicOrIoApis`). **No `Guid.NewGuid`, `Guid.CreateVersion7` or `GetHashCode(`** (`Core_DoesNotCreateIdentifiersOrUseHashCodes`). Logging and DI are also out (no `Microsoft.*`). Randomness goes through `DeterministicRandom`, whose state lives in `GameState.RngState`; new ids come from `GameState.NextId`. The same state, actor and command must always give the same result; order-dependent code sorts explicitly.

## State and rules

- `GameState` and everything in it are immutable records. Change them with `with` and the `internal` `GameState.WithUnit`/`WithoutUnit`/`WithCity`/`WithPlayer`/`AllocateId` helpers. Collections are `ImmutableArray` so the JSON stays plain.
- **Only `GameEngine.Execute` changes a game.** Adding an action means:
  1. one `Actions/XxxAction.cs` holding the `XxxCommand` record and the `internal static class XxxRule` with `Apply(state, actor, command) → CommandResult`;
  2. its `[JsonDerivedType]` on `Engine/GameCommand`;
  3. a case in `GameEngine.Dispatch`;
  4. new events in `Events/XxxEvent` plus `[JsonDerivedType]` on `GameEvent`, and new codes in `Engine/GameErrors`.
- **Rule violations are returned as `GameErrors.Xxx`** (a stable snake_case code) and never thrown. Throw only for programming errors: an invalid `GameSetup`, an unknown command type, a missing entity inside a rule.
- **New unit or terrain stats** go in `UnitCatalog` / `TerrainCatalog` as data, not in `if` chains inside rules.
- **Clients only ever receive `GameEngine.ViewFor`** (`PlayerView`), never `GameState` or unfiltered events. Anything added to state that an opponent must not see must be filtered in `VisibilityCalculator.ViewFor`.
- **Rejections must not leak hidden information.** Another player's unit or city is `*.not_found`. A rule that inspects tiles or objects the actor cannot see must return the same error it would return if they were empty. Keep `UnitCatalogTests`' invariants (sight ≥ movement; settler sight ≥ city spacing − 1) or filter by visibility.
- **Enum values from commands are untrusted:** check `Enum.IsDefined` in the rule before any catalog lookup, otherwise the catalog throws and the Api returns 500.

## Serialization

- JSON goes through `GameSerializer.Options` only.
- `type` discriminators (`move_unit`, `unit_moved`, …) are a storage and wire format: never rename one.
- Computed properties get `[JsonIgnore]`.
- A breaking change to the shape of `GameState` bumps `GameState.CurrentSchemaVersion` and needs a migration for stored games.

## Folders and naming

Whitelisted by `Templates.Core()` and checked by `FolderStructureTests`:

| Folder | Holds |
|---|---|
| `Engine/` | The pipeline: `GameEngine`, `CommandResult`, `GameUpdate`, `GameCommand` (base + discriminators), `GameError`/`GameErrors`, the cross-action `CommandGuardRule` and `VictoryRule`, `DeterministicRandom` |
| `State/` | The snapshot: `GameState`, `GameStatus`, `GameSetup`, `GameFactory`, `Player`, `PlayerId`, `PlayerStatus` |
| `Map/` | Geometry and movement: `HexCoord`, `GameMap`, `Tile`, terrain types and catalog, `ReachabilityCalculator` |
| `Units/`, `Cities/` | The things on the map and the unit catalog |
| `Actions/` | Flat; one `XxxAction.cs` per player action with exactly `XxxCommand` + `XxxRule` (`ActionFiles_DeclareExactlyThePrefixedCommandAndRule`) |
| `Events/` | `GameEvent` + events; shared by several actions, so not per action. Types end with `Event` |
| `Visibility/` | Fog of war: `VisibilityCalculator`, `PlayerView` and its parts |
| `Serialization/` | `GameSerializer` |

- There are no root files.
- Concept folders may get sub-folders when they grow.
- A new top-level folder needs a template change: update `Templates.CoreFolders`, this table and `architecture.md` together.

## Tests

- **Location:** `tests/Govern.UnitTests/Core/<Folder>/XxxTests.cs`.
- **Rules:** tested through `GameEngine.Execute`, with scenarios from `Builders/GameStateBuilder`. Give both players a city or settler (e.g. `WithCapitals()`), otherwise `VictoryRule` ends the game after the first command.
- **Assertions:** `result.ShouldSucceed()` / `result.ShouldFailWith(GameErrors.X)`.
- **Records holding `ImmutableArray` don't compare structurally.** Compare their elements, or the `GameSerializer` JSON.
