using Govern.Client.Helpers;
using Govern.Client.Models;
using Govern.Core.Cities;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The selected unit's panel: its combat stats for any unit the player sees, and its movement, sight, cost, build production
/// and job for their own.
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

    // How much a turn adds to the job: the unit's own build production, plus the production of the own city whose land it
    // stands on (if that city is not feeding an older job, which the view doesn't tell, the job may take longer).
    private int JobPerTurn
    {
        get
        {
            var perTurn = Ruleset.Unit(Unit.Type).BuildProduction;
            var city = View.Cities.FirstOrDefault(city => city.Owner == View.Viewer && WorkArea.Covers(Ruleset, city.Position, Unit.Position));
            return city is null ? perTurn : perTurn + WorkArea.ProductionPerTurn(Ruleset, city.BuildingList);
        }
    }

    private string JobText(UnitJob job) => job.Improvement is { } improvement
        ? $"Building a {Ruleset.ImprovementName(improvement)}: {job.Progress} of {JobCost(job)}"
        : $"Building a bridge: {job.Progress} of {JobCost(job)}";

    private int JobCost(UnitJob job) => job.Improvement is { } improvement ? Ruleset.Improvement(improvement).Cost : Ruleset.Bridges.Cost;

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
