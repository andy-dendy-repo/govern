using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit finished a bridge over the river between two neighbouring tiles.
/// </summary>
/// <param name="UnitId">The unit that built it.</param>
/// <param name="Owner">The unit's owner.</param>
/// <param name="From">The unit's tile.</param>
/// <param name="To">The tile across the river.</param>
public sealed record BridgeBuiltEvent(UnitId UnitId, PlayerId Owner, HexCoord From, HexCoord To) : GameEvent;
