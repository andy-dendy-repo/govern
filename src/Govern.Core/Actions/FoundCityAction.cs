using System.Globalization;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
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

        if (Check(state, settler) is { } violation)
        {
            return violation;
        }

        var (allocated, id) = state.AllocateId();
        var name = string.Create(CultureInfo.InvariantCulture, $"City {state.Cities.Count(city => city.Owner == actor) + 1}");
        var city = new City(new CityId(id), actor, name, settler.Position, UnitType.Soldier, ProductionProgress: 0);
        var founded = allocated.WithoutUnit(settler.Id);

        return new GameUpdate(founded with { Cities = founded.Cities.Add(city) }, [new CityFoundedEvent(city, settler.Id)]);
    }

    private static GameError? Check(GameState state, Unit settler)
    {
        if (!settler.Definition.CanFoundCity)
        {
            return GameErrors.CannotFoundCity;
        }

        if (settler.MovementLeft <= 0)
        {
            return GameErrors.NoMovementLeft;
        }

        if (state.CityAt(settler.Position) is not null)
        {
            return GameErrors.CityTileTaken;
        }

        return state.Cities.Any(city => city.Position.DistanceTo(settler.Position) < City.MinimumSpacing)
            ? GameErrors.CityTooClose
            : null;
    }
}
