using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Events;

/// <summary>
/// A city finished producing a building.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="Building">The building.</param>
/// <param name="Position">The tile of the city's territory the building stands on.</param>
public sealed record BuildingCompletedEvent(CityId CityId, BuildingType Building, HexCoord Position) : GameEvent;
