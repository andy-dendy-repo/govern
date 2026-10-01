using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit finished an improvement on its tile.
/// </summary>
/// <param name="UnitId">The unit that built it.</param>
/// <param name="Owner">The unit's owner.</param>
/// <param name="Position">The improved tile.</param>
/// <param name="Improvement">The improvement.</param>
public sealed record ImprovementBuiltEvent(UnitId UnitId, PlayerId Owner, HexCoord Position, ImprovementType Improvement) : GameEvent;
