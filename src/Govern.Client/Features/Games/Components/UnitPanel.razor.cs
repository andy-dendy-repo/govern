using System.Collections.Immutable;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Helpers;
using Govern.Client.Models;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The selected unit's panel: its combat stats for any unit the player sees, and its movement, sight, cost, build production,
/// job, destination, upkeep and supply for their own.
/// </summary>
public partial class UnitPanel
{
    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The selected unit.</summary>
    [Parameter, EditorRequired]
    public UnitView Unit { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    private bool IsOwn => Unit.Owner == View.Viewer;

    private ImmutableArray<ResourceAmount> Upkeep => Ruleset.Unit(Unit.Type).Upkeep;

    // Who keeps the unit up at the end of the turn, or what it will lack and what that costs it.
    private string SupplyText(UnitSupply supply)
    {
        var city = supply.City is { } id ? View.Cities.FirstOrDefault(city => city.Id == id)?.Name : null;
        var loss = $"it loses {Ruleset.Upkeep.ShortageDamage} health at the end of the turn";
        return (supply.Supplied, city) switch
        {
            (true, { } name) => $"Kept up by {name}.",
            (true, null) => "Fed by the land around it.",
            (false, { } name) => $"{name} can't pay its upkeep: {loss}.",
            (false, null) => $"The land around it lacks {ResourceFormatter.Describe(Ruleset, supply.Shortfall)}: {loss}.",
        };
    }

    private StatRow[] Rows
    {
        get
        {
            var definition = Ruleset.Unit(Unit.Type);
            if (!IsOwn)
            {
                return [.. CombatRows(definition)];
            }

            StatRow[] rows =
            [
                .. CombatRows(definition),
                new(Icons.Movement, "Movement", $"{Unit.MovementLeft} of {definition.Movement}",
                    $"Movement left this turn: {Unit.MovementLeft}. Each turn starts with {definition.Movement}.", "movement"),
                SightRow(definition.Sight),
                new(Icons.Production, "Cost", $"{definition.ProductionCost}", $"A city needs {definition.ProductionCost} production to build one.", "cost"),
            ];
            return definition.BuildProduction > 0
                ? [.. rows, new(Icons.Production, "Build", $"{definition.BuildProduction} per turn",
                    $"Adds {definition.BuildProduction} production a turn to what it builds; on your city's land the city's production is added too.",
                    "build")]
                : rows;
        }
    }

    // How much a turn adds to the job: the unit's own build production, plus the production of the own city that feeds it
    // (the city whose ground it stands on, unless that city feeds an older job).
    private int JobPerTurn
    {
        get
        {
            var perTurn = Ruleset.Unit(Unit.Type).BuildProduction;
            return JobFundingResolver.FeedingCities(Ruleset, View).TryGetValue(Unit.Id, out var city)
                ? perTurn + CityProduction.PerTurn(Ruleset, city.WorkingBuildings(Ruleset.Upkeep.BuildingHealth))
                : perTurn;
        }
    }

    // The standing order: how many turns the walk still needs (a standing order walks on at the start of each turn), or that no
    // known way leads there now (the order then ends at the start of the next turn).
    private string DestinationText =>
        !View.MayAct ? "Walking to its destination: it walks on at the start of your turn."
        : GameEngine.DestinationPath(Ruleset, View, Unit.Id, Unit.Destination!.Value) is { } route
            ? $"Walking to its destination: {Turns(Math.Max(1, route.Turns))} away. Stop or any other order cancels it."
            : "Walking to its destination, but no known way leads there now.";

    private static string Turns(int turns) => turns == 1 ? "1 turn" : $"{turns} turns";

    private string JobText(UnitJob job) => job switch
    {
        { Improvement: { } improvement } => $"Building a {Ruleset.ImprovementName(improvement)}: {job.Progress} of {JobCost(job)}",
        { UpgradeTo: { } upgrade } when job.Progress >= JobCost(job) =>
            $"Upgrading to a {Ruleset.BuildingName(upgrade)}: done, waiting for the city to pay {ResourceFormatter.Describe(Ruleset, Ruleset.Building(upgrade).ResourceCost)}",
        { UpgradeTo: { } upgrade } => $"Upgrading to a {Ruleset.BuildingName(upgrade)}: {job.Progress} of {JobCost(job)}",
        _ => $"Building a bridge: {job.Progress} of {JobCost(job)}",
    };

    private int JobCost(UnitJob job) => job switch
    {
        { Improvement: { } improvement } => Ruleset.Improvement(improvement).Cost,
        { UpgradeTo: { } upgrade } => Ruleset.Building(upgrade).Cost,
        _ => Ruleset.Bridges.Cost,
    };

    // Sight plus the bonus of the tile the unit stands on (hills); the own unit's tile is always visible, so its terrain is known.
    private StatRow SightRow(int sight)
    {
        if (View.TileAt(Unit.Position).Terrain is { } terrain && Ruleset.Terrain(terrain).SightBonus is > 0 and var bonus)
        {
            return new(Icons.Sight, "Sight", $"{sight + bonus}",
                $"Sees {sight + bonus} tiles around it: {sight}, plus {bonus} for standing on {Ruleset.TerrainName(terrain)}.", "sight");
        }

        return new(Icons.Sight, "Sight", $"{sight}", $"Sees {sight} tiles around it.", "sight");
    }

    // Strength (and ranged strength) with the class in the detail; a civilian shows that it can't fight.
    private IEnumerable<StatRow> CombatRows(UnitDefinition definition)
    {
        var className = Ruleset.ClassName(definition.Class);
        if (definition.Strength is not { } strength)
        {
            yield return new(Icons.Strength, "Strength", "—", $"{className}: can't fight, and any attack destroys it.", "strength");
            yield break;
        }

        yield return new(Icons.Strength, "Strength", $"{strength}",
            $"{className}. Fights at up to {strength}; a hurt unit loses up to half of it.", "strength");
        if (definition.Ranged is { } ranged)
        {
            yield return new(Icons.Strength, "Ranged", $"{ranged.Strength}, range {ranged.Range}",
                $"Shoots units up to {ranged.Range} tiles away at strength {ranged.Strength}; they can't hit back.", "ranged");
        }
    }
}
