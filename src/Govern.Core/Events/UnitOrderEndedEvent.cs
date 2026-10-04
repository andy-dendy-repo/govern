using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit's order to walk to a destination, or to explore, ended on its own (not by its owner's command). Only the owner receives it.
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="UnitType">Its type.</param>
/// <param name="Position">Where the unit stands now.</param>
/// <param name="Destination">The destination it was walking to; for an exploring unit, the tile it stopped on.</param>
/// <param name="Reason">Why the order ended.</param>
public sealed record UnitOrderEndedEvent(
    UnitId UnitId,
    PlayerId Owner,
    UnitType UnitType,
    HexCoord Position,
    HexCoord Destination,
    OrderEndReason Reason) : GameEvent;
