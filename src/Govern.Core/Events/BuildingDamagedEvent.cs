using Govern.Core.Cities;
using Govern.Core.Economy;

namespace Govern.Core.Events;

/// <summary>A city couldn't pay a building's upkeep this turn, so the building lost health (at 0 it gives nothing).</summary>
/// <param name="CityId">The city.</param>
/// <param name="Building">The building.</param>
/// <param name="Health">Its health afterwards.</param>
public sealed record BuildingDamagedEvent(CityId CityId, BuildingType Building, int Health) : GameEvent;
