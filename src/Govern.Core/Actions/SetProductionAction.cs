using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Chooses which unit a city builds; accumulated progress is kept.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="UnitType">Unit type to build.</param>
public sealed record SetProductionCommand(CityId CityId, UnitType UnitType) : GameCommand;

/// <summary>
/// Changes what a city builds, keeping the accumulated progress.
/// </summary>
internal static class SetProductionRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, SetProductionCommand command)
    {
        var city = state.FindCity(command.CityId);
        if (city is null || city.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        if (!Enum.IsDefined(command.UnitType))
        {
            return RuleViolations.UnknownUnitType;
        }

        var changed = city with { Producing = command.UnitType };
        return new GameUpdate(state.WithCity(changed), [new ProductionChangedEvent(city.Id, command.UnitType)]);
    }
}
