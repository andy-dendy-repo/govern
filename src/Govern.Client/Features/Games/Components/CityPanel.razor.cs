using System.Collections.Immutable;
using System.Globalization;
using Govern.Client.Features.Games.Helpers;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The selected city's panel: its name, and for the player's own city what it produces, the units and buildings it can
/// produce, and the buildings it has.
/// </summary>
public partial class CityPanel
{
    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The selected city.</summary>
    [Parameter, EditorRequired]
    public CityView City { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Raised with the command a button sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    private bool CanAct => View.Status == GameStatus.InProgress && View.CurrentPlayer == View.Viewer;

    // While the city lends its production to a job on its ground, its own production gets nothing.
    private bool FeedsAJob => JobFundingResolver.FeedingCities(Ruleset, View).Values.Any(city => city.Id == City.Id);

    private int PerTurn => FeedsAJob ? 0 : CityProduction.PerTurn(Ruleset, City.BuildingList);

    // What the city adds to its stock at the end of the turn (its territory, its own yields and its buildings).
    private ImmutableArray<ResourceAmount> Income => GameEngine.CityIncome(Ruleset, View, City.Id);

    // The price of the next tile in the growth resource, against what the city holds of it.
    private string TerritoryText
    {
        get
        {
            var territory = Ruleset.Territory;
            var price = territory.NextTileCost(City.TilesGained);
            var held = YieldCalculator.AmountOf(City.StockList, territory.GrowthResource);
            return $"Next territory tile: {held} of {price} {Ruleset.ResourceName(territory.GrowthResource).ToLowerInvariant()}";
        }
    }

    private static string Gain(int gain) => gain switch
    {
        > 0 => $"+{gain.ToString(CultureInfo.InvariantCulture)}",
        < 0 => gain.ToString(CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private int? ProducingCost => City.ProducingBuilding is { } building
        ? Ruleset.Building(building).Cost
        : City.Producing is { } type ? Ruleset.Unit(type).ProductionCost : null;

    private string ProductionText => (City.ProducingBuilding, City.Producing) switch
    {
        ({ } building, _) => $"Producing a {Ruleset.BuildingName(building)}: {City.ProductionProgress ?? 0} of {ProducingCost}",
        (_, { } type) => $"Producing a {Ruleset.UnitName(type)}: {City.ProductionProgress ?? 0} of {ProducingCost}",
        _ => "Producing nothing",
    };

    // A unit button is pressed only while no building replaces the unit in production.
    private bool ProducingUnit(UnitType type) => City.ProducingBuilding is null && City.Producing == type;

    // What a building gives: its yields each turn, any extra production, and the terrain it needs next to the city.
    private string Effect(BuildingDefinition building)
    {
        var parts = building.Yields.Select(amount => $"+{amount.Amount} {Ruleset.ResourceName(amount.Resource)}").ToList();
        if (building.Production > 0)
        {
            parts.Add($"+{building.Production} production");
        }

        var effect = $"{string.Join(", ", parts)} a turn";
        return building.RequiresAdjacentTerrain.IsEmpty
            ? effect
            : $"{effect}; needs {string.Join(" or ", building.RequiresAdjacentTerrain.Select(Ruleset.TerrainName))} next to the city";
    }
}
