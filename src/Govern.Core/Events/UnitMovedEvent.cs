using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit moved.
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Path">Tiles entered, in order; the last one is the destination.</param>
public sealed record UnitMovedEvent(UnitId UnitId, ImmutableArray<HexCoord> Path) : GameEvent;
