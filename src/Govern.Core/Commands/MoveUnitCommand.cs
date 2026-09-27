using Govern.Core.Hexes;
using Govern.Core.Units;

namespace Govern.Core.Commands;

/// <summary>
/// Moves a unit along the cheapest path to an empty tile within its remaining movement.
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
/// <param name="Target">Destination tile.</param>
public sealed record MoveUnitCommand(UnitId UnitId, HexCoord Target) : GameCommand;
