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
/// <see cref="ProducingBuilding"/> to <see cref="Buildings"/>, puts it on the <see cref="ProducingBuildingSite"/> tile and goes
/// back to its unit.
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
/// <param name="ProductionPaid">
/// Whether the city has paid the resource cost of what it produces now; it pays when production begins, and the progress grows
/// only once paid.
/// </param>
/// <param name="UpkeepPriority">Whether the city pays its buildings' or its units' upkeep first.</param>
/// <param name="BuildingDamage">Health its buildings have lost to unpaid upkeep; <see langword="null"/> when none is hurt.</param>
/// <param name="ProducingBuildingSite">
/// The tile of the city's territory reserved for <paramref name="ProducingBuilding"/>, where it will stand; set exactly when
/// <paramref name="ProducingBuilding"/> is.
/// </param>
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int TilesGained = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ProductionPaid = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] UpkeepPriority UpkeepPriority = UpkeepPriority.BuildingsFirst,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<BuildingDamage>? BuildingDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HexCoord? ProducingBuildingSite = null)
{
    /// <summary>The city's resources (empty when it has none).</summary>
    [JsonIgnore]
    public ImmutableArray<ResourceAmount> StockList => Stock ?? [];

    /// <summary>The buildings in the city (empty when there are none).</summary>
    [JsonIgnore]
    public ImmutableArray<BuildingType> BuildingList => Buildings ?? [];

    /// <summary>The health each hurt building has lost (empty when none is hurt).</summary>
    [JsonIgnore]
    public ImmutableArray<BuildingDamage> BuildingDamageList => BuildingDamage ?? [];

    /// <summary>Health a building has lost.</summary>
    /// <param name="building">The building.</param>
    /// <returns>0 when it is unhurt (or the city doesn't have it).</returns>
    public int DamageOf(BuildingType building) => BuildingDamageList.FirstOrDefault(entry => entry.Building == building)?.Damage ?? 0;

    /// <summary>The buildings that still work: those with health left.</summary>
    /// <param name="buildingHealth">A building's full health (<see cref="Rulesets.UpkeepRules.BuildingHealth"/>).</param>
    /// <returns>The buildings, in the order they were finished.</returns>
    public IEnumerable<BuildingType> WorkingBuildings(int buildingHealth) => BuildingList.Where(building => DamageOf(building) < buildingHealth);

    /// <summary>Whether the city has a building.</summary>
    /// <param name="building">The building.</param>
    /// <returns><see langword="true"/> when it has been finished in this city.</returns>
    public bool Has(BuildingType building) => Buildings?.Contains(building) == true;
}
