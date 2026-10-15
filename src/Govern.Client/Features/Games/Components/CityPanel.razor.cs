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

    private bool CanAct => View.Status == GameStatus.InProgress && View.MayAct;

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

    // Whether an offer waits at this city for the player's answer (the Market tab is marked).
    private bool HasProposal => View.TradeDealList.Any(deal => deal.Answerer == View.Viewer && deal.AnsweringCity == City.Id);

    // What a resource gains (or loses) at the end of the turn: the city's income less the upkeep it pays and any resource
    // cost it pays to begin its item.
    private int Net(CityForecast? forecast, ResourceType resource) => forecast is null
        ? 0
        : YieldCalculator.AmountOf(forecast.Income, resource)
            - YieldCalculator.AmountOf(forecast.Upkeep.Paid, resource)
            - (PaysThisTurn(forecast) ? YieldCalculator.AmountOf(ResourceCost, resource) : 0);

    // How much of a resource the city can hold (int.MaxValue for one it doesn't stock); the caps after this turn's upkeep.
    private int CapOf(CityForecast? forecast, ResourceType resource) =>
        CityStorage.CapOf(forecast?.Storage ?? CityStorage.Caps(Ruleset, City.WorkingBuildings(Ruleset.Upkeep.BuildingHealth)), resource);

    // What of a resource won't fit in the city's storage at the end of the turn. The forecast assumes the city pays its item's
    // cost; a city feeding a job doesn't, so then it is the stock plus its net gain over the cap (an item's cost never uses the
    // growth resource, whose loss also depends on the tile it buys, so that one always comes from the forecast).
    private int LostOf(CityForecast? forecast, ResourceType resource, int amount, int gain, int cap) => forecast switch
    {
        null => 0,
        _ when FeedsAJob && resource != Ruleset.Territory.GrowthResource && cap != int.MaxValue => Math.Max(0, amount + gain - cap),
        _ => YieldCalculator.AmountOf(forecast.Lost, resource),
    };

    private string StorageTitle(ResourceType resource, int amount, int cap, int lost)
    {
        var name = Ruleset.ResourceName(resource);
        var held = cap == int.MaxValue ? $"{amount} {name}" : $"{amount} of {cap} {name} the city can hold";
        return lost > 0 ? $"{held}; {lost} won't fit at the end of the turn and is lost" : held;
    }

    // Where the city's production per turn comes from: its own, then each working building that adds some.
    private string SourcesText
    {
        get
        {
            var sources = CityProduction.Sources(Ruleset, City.WorkingBuildings(Ruleset.Upkeep.BuildingHealth));
            var parts = sources.Select(source => $"{(source.Building is { } building ? Ruleset.BuildingName(building) : "city")} {source.Amount}");
            return $"Production {sources.Sum(static source => source.Amount)} a turn: {string.Join(", ", parts)}";
        }
    }

    // The job this city's production goes to this turn instead of its own item.
    private string LentText
    {
        get
        {
            var fed = JobFundingResolver.FeedingCities(Ruleset, View).FirstOrDefault(entry => entry.Value.Id == City.Id).Key;
            var unit = View.Units.FirstOrDefault(unit => unit.Id == fed);
            var job = unit?.Job switch
            {
                { Improvement: { } improvement } => $"the {Ruleset.UnitName(unit.Type)}'s {Ruleset.ImprovementName(improvement)}",
                { UpgradeTo: { } building } => $"the {Ruleset.UnitName(unit.Type)}'s {Ruleset.BuildingName(building)} upgrade",
                { BridgeTo: not null } => $"the {Ruleset.UnitName(unit.Type)}'s bridge",
                _ => "a unit building on its land",
            };
            return $"This turn the city's production goes to {job}, not to its own item.";
        }
    }

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

    // The building the city lacks to produce a unit, or null (the engine's checks): a trader needs a trade post, a ship its
    // harbour, each or an upgrade of it.
    private BuildingType? MissingBuilding(UnitDefinition unit) => unit switch
    {
        { CanTrade: true } when !HasKindOf(Ruleset.Trade.Building) => Ruleset.Trade.Building,
        { RequiresBuilding: { } needed } when !HasKindOf(needed) => needed,
        _ => null,
    };

    // Whether the unit trades and the city already has as many traders as it may (the engine's check).
    private bool TradersFull(UnitDefinition unit) => unit.CanTrade
        && View.Units.Count(other => other.Owner == View.Viewer && other.HomeCity == City.Id && Ruleset.Unit(other.Type).CanTrade) >= Ruleset.Trade.TradersPerCity;

    // Whether the city has a building or one of its upgrades.
    private bool HasKindOf(BuildingType building) => City.BuildingList.Any(built => Ruleset.IsKindOf(built, building));

    // Why the player's cities can't start a unit (locked by science or replaced by a newer one), or null.
    private RuleViolation? ProduceProblem(UnitType unit) => ScienceCalculator.CheckProduce(Ruleset, View.Science?.NodeList ?? [], unit);

    // A unit button is pressed only while no building replaces the unit in production.
    private bool ProducingUnit(UnitType type) => City.ProducingBuilding is null && City.Producing == type;

    // What a building gives: its yields each turn, any extra production, the storage it adds, and the terrain it needs next to
    // its tile.
    private string Effect(BuildingDefinition building)
    {
        var parts = building.Yields.Select(amount => $"+{amount.Amount} {Ruleset.ResourceName(amount.Resource)}").ToList();
        if (building.Production > 0)
        {
            parts.Add($"+{building.Production} production");
        }

        var effect = parts.Count > 0 ? $"{string.Join(", ", parts)} a turn" : null;
        if (!building.Storage.IsEmpty)
        {
            var storage = $"holds {string.Join(", ", building.Storage.Select(amount => $"+{amount.Amount} {Ruleset.ResourceName(amount.Resource)}"))}";
            effect = effect is null ? storage : $"{effect}; {storage}";
        }

        effect ??= "No effect";
        return building.RequiresAdjacentTerrain.IsEmpty
            ? effect
            : $"{effect}; needs {string.Join(" or ", building.RequiresAdjacentTerrain.Select(Ruleset.TerrainName))} next to its tile";
    }
}
