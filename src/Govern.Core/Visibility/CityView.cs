using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A city as a player sees it. Production, buildings and stock are shown only for the viewer's own cities: an enemy's progress
/// would reveal when their next unit appears, and its buildings, stock and upkeep what it yields and can afford.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">Player who controls the city.</param>
/// <param name="Name">Display name.</param>
/// <param name="Position">Tile the city stands on.</param>
/// <param name="Producing">Unit type the city is building; <see langword="null"/> for an enemy city.</param>
/// <param name="ProductionProgress">Production points towards what it produces; <see langword="null"/> for an enemy city.</param>
/// <param name="ProducingBuilding">The building it produces now, if any; always <see langword="null"/> for an enemy city.</param>
/// <param name="Buildings">The city's buildings; <see langword="null"/> when there are none or for an enemy city.</param>
/// <param name="Stock">The city's resources; <see langword="null"/> when it has none or for an enemy city.</param>
/// <param name="TilesGained">Tiles the city has claimed since it was founded; 0 for an enemy city.</param>
/// <param name="ProductionPaid">Whether the resource cost of what it produces is paid; <see langword="false"/> for an enemy city.</param>
/// <param name="UpkeepPriority">Whether it pays buildings or units first; the default for an enemy city.</param>
/// <param name="BuildingDamage">Health its buildings have lost; <see langword="null"/> when none is hurt or for an enemy city.</param>
public sealed record CityView(
    CityId Id,
    PlayerId Owner,
    string Name,
    HexCoord Position,
    UnitType? Producing,
    int? ProductionProgress,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingType? ProducingBuilding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<BuildingType>? Buildings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceAmount>? Stock = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int TilesGained = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ProductionPaid = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] UpkeepPriority UpkeepPriority = UpkeepPriority.BuildingsFirst,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<BuildingDamage>? BuildingDamage = null)
{
    /// <summary>The city's resources (empty when it has none or for an enemy city).</summary>
    [JsonIgnore]
    public ImmutableArray<ResourceAmount> StockList => Stock ?? [];

    /// <summary>The buildings the viewer knows of (empty for an enemy city).</summary>
    [JsonIgnore]
    public ImmutableArray<BuildingType> BuildingList => Buildings ?? [];

    /// <summary>Health a building has lost (0 when unhurt, or for an enemy city).</summary>
    /// <param name="building">The building.</param>
    /// <returns>The damage.</returns>
    public int DamageOf(BuildingType building) => BuildingDamage?.FirstOrDefault(entry => entry.Building == building)?.Damage ?? 0;

    /// <summary>The buildings that still work: those with health left.</summary>
    /// <param name="buildingHealth">A building's full health (<see cref="Rulesets.UpkeepRules.BuildingHealth"/>).</param>
    /// <returns>The buildings, in the order they were finished.</returns>
    public IEnumerable<BuildingType> WorkingBuildings(int buildingHealth) => BuildingList.Where(building => DamageOf(building) < buildingHealth);

    /// <summary>Projects a city for a viewer, hiding the production and buildings of cities the viewer doesn't own.</summary>
    /// <param name="city">The city.</param>
    /// <param name="viewer">The player the view is for.</param>
    /// <returns>The city as the viewer sees it.</returns>
    public static CityView From(City city, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(city);
        return city.Owner == viewer
            ? new CityView(city.Id, city.Owner, city.Name, city.Position, city.Producing, city.ProductionProgress, city.ProducingBuilding, city.Buildings, city.Stock, city.TilesGained, city.ProductionPaid, city.UpkeepPriority, city.BuildingDamage)
            : new CityView(city.Id, city.Owner, city.Name, city.Position, Producing: null, ProductionProgress: null);
    }
}
