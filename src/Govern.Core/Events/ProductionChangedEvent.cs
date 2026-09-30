using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A city switched what it builds.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="UnitType">What it builds now.</param>
public sealed record ProductionChangedEvent(CityId CityId, UnitType UnitType) : GameEvent;
