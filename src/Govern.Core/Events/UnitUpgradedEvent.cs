using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit was upgraded into the next version of its line. Seen by its owner and by players who see its tile.
/// </summary>
/// <param name="UnitId">The unit (it keeps its id).</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Position">Its tile.</param>
/// <param name="From">Its type before.</param>
/// <param name="To">Its type now.</param>
public sealed record UnitUpgradedEvent(UnitId UnitId, PlayerId Owner, HexCoord Position, UnitType From, UnitType To) : GameEvent;
