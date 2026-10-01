using System.Collections.Immutable;
using Govern.Client.Features.Games.Helpers;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The upkeep of the player's own city: what it pays a turn, what it can't pay, and whether it pays its buildings or its units
/// first (<see cref="Core.Actions.SetUpkeepPriorityCommand"/>).
/// </summary>
public partial class CityUpkeep
{
    private static readonly (UpkeepPriority Priority, string Label)[] Priorities =
    [
        (UpkeepPriority.BuildingsFirst, "Buildings"),
        (UpkeepPriority.UnitsFirst, "Units"),
    ];

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The player's city.</summary>
    [Parameter, EditorRequired]
    public CityView City { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The city's end-of-turn forecast (<see cref="GameEngine.CityForecast"/>).</summary>
    [Parameter, EditorRequired]
    public CityForecast? Forecast { get; set; }

    /// <summary>Whether a command is in flight (the switch is disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Whether the player may act now (their turn, game in progress).</summary>
    [Parameter]
    public bool CanAct { get; set; }

    /// <summary>Raised with the command the switch sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    // Everything the city's buildings and the units on its territory need, paid or not.
    private ImmutableArray<ResourceAmount> Due => Forecast is { Upkeep: var upkeep }
        ? YieldCalculator.Sum(Ruleset, upkeep.Paid
            .Concat(upkeep.UnpaidBuildings.SelectMany(building => Ruleset.Building(building).Upkeep))
            .Concat(upkeep.UnpaidUnits.SelectMany(UnitUpkeep)))
        : [];

    // The buildings and units the city can't keep up this turn, and what they lose.
    private string? ShortageText
    {
        get
        {
            if (Forecast is not { Upkeep: var upkeep } || (upkeep.UnpaidBuildings.IsEmpty && upkeep.UnpaidUnits.IsEmpty))
            {
                return null;
            }

            var names = upkeep.UnpaidBuildings.Select(Ruleset.BuildingName)
                .Concat(upkeep.UnpaidUnits.Select(id => View.FindOwnUnit(id) is { } unit ? Ruleset.UnitName(unit.Type) : "a unit"));
            return $"Can't pay for {string.Join(", ", names)}: each loses {Ruleset.Upkeep.ShortageDamage} health at the end of the turn.";
        }
    }

    private IEnumerable<ResourceAmount> UnitUpkeep(Core.Units.UnitId id) =>
        View.FindOwnUnit(id) is { } unit ? Ruleset.Unit(unit.Type).Upkeep : [];
}
