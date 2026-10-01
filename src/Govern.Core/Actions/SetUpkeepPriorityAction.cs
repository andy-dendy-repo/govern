using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>Chooses whether a city pays its buildings' or its units' upkeep first when its stock runs short.</summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="Priority">Buildings first or units first.</param>
public sealed record SetUpkeepPriorityCommand(CityId CityId, UpkeepPriority Priority) : GameCommand;

/// <summary>Sets a city's upkeep order; it takes effect at the end of the owner's turn and stays when the city changes hands.</summary>
internal static class SetUpkeepPriorityRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, SetUpkeepPriorityCommand command)
    {
        var city = state.FindCity(command.CityId);
        if (city is null || city.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        if (!Enum.IsDefined(command.Priority))
        {
            return RuleViolations.UnknownUpkeepPriority;
        }

        var changed = city with { UpkeepPriority = command.Priority };
        return new GameUpdate(state.WithCity(changed), [new UpkeepPrioritySetEvent(city.Id, command.Priority)]);
    }
}
