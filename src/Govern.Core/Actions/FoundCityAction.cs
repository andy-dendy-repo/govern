using System.Globalization;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Founds a city on the settler's tile; the settler is consumed.
/// </summary>
/// <param name="UnitId">The actor's settler.</param>
public sealed record FoundCityCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Turns a settler into a city on its tile, at least <see cref="City.MinimumSpacing"/> away from every other city.
/// </summary>
internal static class FoundCityRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, FoundCityCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var settler, out var error))
        {
            return error;
        }

        if (Check(settler, state.Cities.Select(static city => city.Position)) is { } violation)
        {
            return violation;
        }

        var (allocated, id) = state.AllocateId();
        var name = string.Create(CultureInfo.InvariantCulture, $"City {state.Cities.Count(city => city.Owner == actor) + 1}");
        var city = new City(new CityId(id), actor, name, settler.Position, UnitType.Soldier, ProductionProgress: 0);
        var founded = allocated.WithoutUnit(settler.Id);

        return new GameUpdate(founded with { Cities = founded.Cities.Add(city) }, [new CityFoundedEvent(city, settler.Id)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator). Every city closer than the spacing is visible to the settler
    // (settler sight >= spacing - 1, UnitCatalogTests), so the view gives the same answer as the state.
    public static RuleViolation? Check(Unit settler, IEnumerable<HexCoord> cityPositions)
    {
        if (!settler.Definition.CanFoundCity)
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

        return positions.Any(position => position.DistanceTo(settler.Position) < City.MinimumSpacing)
            ? RuleViolations.CityTooClose
            : null;
    }
}
