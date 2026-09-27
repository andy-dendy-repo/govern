using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A city finished producing a unit.
/// </summary>
/// <param name="Unit">The new unit.</param>
/// <param name="CityId">The city that produced it.</param>
public sealed record UnitCreatedEvent(Unit Unit, CityId CityId) : GameEvent;
