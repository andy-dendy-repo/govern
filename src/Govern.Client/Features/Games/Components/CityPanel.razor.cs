using System.Collections.Immutable;
using System.Globalization;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Services;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The selected city's panel: its name, and for the player's own city its resources, what it produces, and tabs for the
/// units it can produce, its buildings, its economy (upkeep, territory) and its market (trade).
/// </summary>
public partial class CityPanel
{
    /// <summary>The game screen's UI state, which keeps the open tab.</summary>
    [CascadingParameter]
    public GameUiService Ui { get; set; } = null!;

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

    // The production the city's item gains at the end of the turn: none while it feeds a job or can't pay to begin.
    private int PerTurn(CityForecast? forecast) => FeedsAJob ? 0 : forecast?.ProductionPerTurn ?? 0;

    // What the city will do at the end of the turn: its income, its upkeep, and whether its production can begin.
    private CityForecast? Forecast => GameEngine.CityForecast(Ruleset, View, City.Id);

    private ImmutableArray<ResourceAmount> ResourceCost =>
        City.Producing is { } unit ? CityProduction.ResourceCost(Ruleset, City.ProducingBuilding, unit) : [];

    // Whether the city pays its item's resource cost at the end of this turn.
    private bool PaysThisTurn(CityForecast? forecast) =>
        !City.ProductionPaid && !FeedsAJob && forecast is { MissingToBegin.IsEmpty: true } && !ResourceCost.IsEmpty;

    // Whether the city can't pay the upkeep of some of its buildings or units this turn (the Economy tab shows a warning).
    private static bool HasShortage(CityForecast? forecast) =>
        forecast is { Upkeep: var upkeep } && (!upkeep.UnpaidBuildings.IsEmpty || !upkeep.UnpaidUnits.IsEmpty);

    // Whether a proposal waits at this city for the player's answer (the Market tab is marked).
    private bool HasProposal => View.TradeRouteList.Any(route => route.Host == City.Id && route.HostOwner == View.Viewer && route.Deal is { Accepted: false });

    // What a resource gains (or loses) at the end of the turn: the city's income less the upkeep it pays and any resource
    // cost it pays to begin its item.
    private int Net(CityForecast? forecast, ResourceType resource) => forecast is null
        ? 0
        : YieldCalculator.AmountOf(forecast.Income, resource)
            - YieldCalculator.AmountOf(forecast.Upkeep.Paid, resource)
            - (PaysThisTurn(forecast) ? YieldCalculator.AmountOf(ResourceCost, resource) : 0);

    // Whether the resource cost of what the city produces is paid, will be paid at the end of the turn, or what is missing.
    private string? CostText(CityForecast? forecast)
    {
        var cost = ResourceCost;
        return (cost.IsEmpty, City.ProductionPaid, forecast?.MissingToBegin) switch
        {
            (true, _, _) => null,
            (false, true, _) => $"Resources paid: {ResourceFormatter.Describe(Ruleset, cost)}.",
            _ when FeedsAJob => $"Pays {ResourceFormatter.Describe(Ruleset, cost)} to begin once its production is free of the job.",
            (false, false, { IsEmpty: false } missing) => $"Waiting for {ResourceFormatter.Describe(Ruleset, missing)} more to begin.",
            _ => $"Pays {ResourceFormatter.Describe(Ruleset, cost)} at the end of the turn to begin.",
        };
    }

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

    // A trader is trained only in a city with a trade post or an upgrade of it (the engine's check).
    private bool NeedsTradePost(UnitDefinition unit) => unit.CanTrade && !HasKindOf(Ruleset.Trade.Building);

    // Whether the city has a building or one of its upgrades.
    private bool HasKindOf(BuildingType building) => City.BuildingList.Any(built => Ruleset.IsKindOf(built, building));

    // Why the player's cities can't start a unit (locked by science or replaced by a newer one), or null.
    private RuleViolation? ProduceProblem(UnitType unit) => ScienceCalculator.CheckProduce(Ruleset, View.Science?.NodeList ?? [], unit);

    // A unit button is pressed only while no building replaces the unit in production.
    private bool ProducingUnit(UnitType type) => City.ProducingBuilding is null && City.Producing == type;

    // What a building gives: its yields each turn, any extra production, and the terrain it needs next to its tile.
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
            : $"{effect}; needs {string.Join(" or ", building.RequiresAdjacentTerrain.Select(Ruleset.TerrainName))} next to its tile";
    }
}
