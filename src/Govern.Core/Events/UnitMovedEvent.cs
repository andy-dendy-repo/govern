using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit moved. A player who doesn't own the unit receives only the tiles of the path they could see
/// (<see cref="Visibility.VisibilityCalculator.EventsFor"/>).
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="UnitType">Its type.</param>
/// <param name="Path">Tiles entered, in order; the last one is the destination.</param>
public sealed record UnitMovedEvent(UnitId UnitId, PlayerId Owner, UnitType UnitType, ImmutableArray<HexCoord> Path) : GameEvent;
