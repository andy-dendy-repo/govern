using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A city switched what it builds.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="UnitType">The unit it builds (after <paramref name="Building"/>, if any).</param>
/// <param name="Building">The building it produces now, if it switched to one.</param>
public sealed record ProductionChangedEvent(
    CityId CityId,
    UnitType UnitType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingType? Building = null) : GameEvent;
