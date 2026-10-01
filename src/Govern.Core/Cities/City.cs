using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Cities;

/// <summary>
/// A city. At the end of its owner's turn it adds its production (<see cref="Rulesets.CityRules.ProductionPerTurn"/> plus its
/// buildings' <see cref="BuildingDefinition.Production"/>) to <see cref="ProductionProgress"/>, unless a builder used it for a job
/// in the city's territory that turn. When the progress reaches the cost it spawns a <see cref="Producing"/> unit, or adds the
/// <see cref="ProducingBuilding"/> to <see cref="Buildings"/> and goes back to its unit.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">Player who controls the city.</param>
/// <param name="Name">Display name.</param>
/// <param name="Position">Tile the city stands on.</param>
/// <param name="Producing">Unit type the city builds (after its <paramref name="ProducingBuilding"/>, if any).</param>
/// <param name="ProductionProgress">Production points accumulated towards what the city produces now.</param>
/// <param name="ProducingBuilding">
/// The building the city produces now, instead of <paramref name="Producing"/>; <see langword="null"/> while it builds units.
/// </param>
/// <param name="Buildings">The buildings in the city, in the order they were finished; <see langword="null"/> when there are none.</param>
/// <param name="Stock">
/// The city's own resources, in the ruleset's order, without zero amounts; <see langword="null"/> when it has none. The yields of
/// its territory and buildings go in here, and the city spends from it.
/// </param>
/// <param name="TilesGained">How many tiles the city has claimed since it was founded (it sets the price of the next one).</param>
public sealed record City(
    CityId Id,
    PlayerId Owner,
    string Name,
    HexCoord Position,
    UnitType Producing,
    int ProductionProgress,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingType? ProducingBuilding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<BuildingType>? Buildings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceAmount>? Stock = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int TilesGained = 0)
{
    /// <summary>The city's resources (empty when it has none).</summary>
    [JsonIgnore]
    public ImmutableArray<ResourceAmount> StockList => Stock ?? [];

    /// <summary>The buildings in the city (empty when there are none).</summary>
    [JsonIgnore]
    public ImmutableArray<BuildingType> BuildingList => Buildings ?? [];

    /// <summary>Whether the city has a building.</summary>
    /// <param name="building">The building.</param>
    /// <returns><see langword="true"/> when it has been finished in this city.</returns>
    public bool Has(BuildingType building) => Buildings?.Contains(building) == true;
}
