using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Founds a city on the settler's tile; the settler is consumed.
/// </summary>
/// <param name="UnitId">The actor's settler.</param>
public sealed record FoundCityCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Turns a settler into a city on its tile, at least the ruleset's <see cref="CityRules.MinimumSpacing"/> away from every other
/// city. The city is named from the ruleset's template and builds its initial production.
/// </summary>
internal static class FoundCityRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, FoundCityCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var settler, out var error))
        {
            return error;
        }

        if (Check(ruleset, settler, state.Cities.Select(static city => city.Position)) is { } violation)
        {
            return violation;
        }

        var (allocated, id) = state.AllocateId();
        var name = ruleset.CityName(state.Cities.Count(city => city.Owner == actor) + 1);
        var city = new City(new CityId(id), actor, name, settler.Position, ruleset.Cities.InitialProduction, ProductionProgress: 0);
        var founded = allocated.WithoutUnit(settler.Id);

        return new GameUpdate(founded with { Cities = founded.Cities.Add(city) }, [new CityFoundedEvent(city, settler.Id)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator). Every city closer than the spacing is visible to the settler
    // (settler sight >= spacing - 1, checked by RulesetValidator), so the view gives the same answer as the state.
    public static RuleViolation? Check(Ruleset ruleset, Unit settler, IEnumerable<HexCoord> cityPositions)
    {
        if (!ruleset.Unit(settler.Type).CanFoundCity)
        {
            return RuleViolations.CannotFoundCity;
        }

        if (settler.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        var positions = cityPositions.ToList();
        if (positions.Contains(settler.Position))
        {
            return RuleViolations.CityTileTaken;
        }

        return positions.Any(position => position.DistanceTo(settler.Position) < ruleset.Cities.MinimumSpacing)
            ? RuleViolations.CityTooClose
            : null;
    }
}
