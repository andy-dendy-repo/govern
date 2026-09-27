using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.Players;
using Govern.Core.State;

namespace Govern.Core.Rules;

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
            return GameErrors.CityNotFound;
        }

        if (!Enum.IsDefined(command.UnitType))
        {
            return GameErrors.UnknownUnitType;
        }

        var changed = city with { Producing = command.UnitType };
        return new GameUpdate(state.WithCity(changed), [new ProductionChangedEvent(city.Id, command.UnitType)]);
    }
}
