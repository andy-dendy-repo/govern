using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit was removed from the game: brought to 0 health in a fight, or its owner left the game.
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="UnitType">Its type.</param>
/// <param name="Position">Where it stood.</param>
public sealed record UnitDestroyedEvent(UnitId UnitId, PlayerId Owner, UnitType UnitType, HexCoord Position) : GameEvent;
