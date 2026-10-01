using Govern.Core.Cities;
using Govern.Core.Economy;

namespace Govern.Core.Events;

/// <summary>
/// A city finished producing a building.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="Building">The building.</param>
public sealed record BuildingCompletedEvent(CityId CityId, BuildingType Building) : GameEvent;
