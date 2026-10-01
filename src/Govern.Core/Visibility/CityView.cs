using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A city as a player sees it. Production and buildings are shown only for the viewer's own cities: an enemy's progress would
/// reveal when their next unit appears, and its buildings what it yields.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">Player who controls the city.</param>
/// <param name="Name">Display name.</param>
/// <param name="Position">Tile the city stands on.</param>
/// <param name="Producing">Unit type the city is building; <see langword="null"/> for an enemy city.</param>
/// <param name="ProductionProgress">Production points towards what it produces; <see langword="null"/> for an enemy city.</param>
/// <param name="ProducingBuilding">The building it produces now, if any; always <see langword="null"/> for an enemy city.</param>
/// <param name="Buildings">The city's buildings; <see langword="null"/> when there are none or for an enemy city.</param>
public sealed record CityView(
    CityId Id,
    PlayerId Owner,
    string Name,
    HexCoord Position,
    UnitType? Producing,
    int? ProductionProgress,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingType? ProducingBuilding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<BuildingType>? Buildings = null)
{
    /// <summary>The buildings the viewer knows of (empty for an enemy city).</summary>
    [JsonIgnore]
    public ImmutableArray<BuildingType> BuildingList => Buildings ?? [];

    /// <summary>Projects a city for a viewer, hiding the production and buildings of cities the viewer doesn't own.</summary>
    /// <param name="city">The city.</param>
    /// <param name="viewer">The player the view is for.</param>
    /// <returns>The city as the viewer sees it.</returns>
    public static CityView From(City city, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(city);
        return city.Owner == viewer
            ? new CityView(city.Id, city.Owner, city.Name, city.Position, city.Producing, city.ProductionProgress, city.ProducingBuilding, city.Buildings)
            : new CityView(city.Id, city.Owner, city.Name, city.Position, Producing: null, ProductionProgress: null);
    }
}
