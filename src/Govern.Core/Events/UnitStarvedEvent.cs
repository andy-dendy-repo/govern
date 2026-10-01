using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit's upkeep wasn't paid this turn (its city's stock or the land around it fell short), so it lost health; a shortage
/// never takes a unit below 1 health.
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Health">Its health afterwards.</param>
public sealed record UnitStarvedEvent(UnitId UnitId, int Health) : GameEvent;
