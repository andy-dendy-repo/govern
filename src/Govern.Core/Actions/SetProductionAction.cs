using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Chooses which unit a city builds (and stops producing a building, if it was); accumulated progress is kept. The unit must be
/// unlocked by the player's science and the newest unlocked version of its line. A resource cost
/// already paid for the item it leaves goes back to the city's stock.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="UnitType">Unit type to build: a unit id of the game's ruleset.</param>
public sealed record SetProductionCommand(CityId CityId, UnitType UnitType) : GameCommand;

/// <summary>
/// Changes what a city builds, keeping the accumulated progress.
/// </summary>
internal static class SetProductionRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, SetProductionCommand command)
    {
        var city = state.FindCity(command.CityId);
        if (city is null || city.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        if (ruleset.FindUnit(command.UnitType) is not { } unit)
        {
            return RuleViolations.UnknownUnitType;
        }

        if (ScienceCalculator.CheckProduce(ruleset, state.FindPlayer(actor)!.NodeList, unit.Id) is { } locked)
        {
            return locked;
        }

        // A trader is trained only where it can trade from: a city with a trade post (or one of its upgrades).
        if (unit.CanTrade && !city.BuildingList.Any(building => ruleset.IsKindOf(building, ruleset.Trade.Building)))
        {
            return RuleViolations.NoTradePost;
        }

        // A city has at most trade.tradersPerCity traders (those that call it home).
        if (unit.CanTrade && TradeRule.TradersOf(ruleset, state, city.Id) >= ruleset.Trade.TradersPerCity)
        {
            return RuleViolations.TooManyTraders;
        }

        // A ship is built only where it can be launched: a city with a harbour (or one of its upgrades).
        if (unit.RequiresBuilding is { } needed && !city.BuildingList.Any(building => ruleset.IsKindOf(building, needed)))
        {
            return RuleViolations.MissingBuilding;
        }

        var changed = city.ProducingBuilding is null && city.Producing == command.UnitType
            ? city
            : CityProduction.Refund(ruleset, city) with { Producing = command.UnitType, ProducingBuilding = null, ProducingBuildingSite = null };
        return new GameUpdate(state.WithCity(changed), [new ProductionChangedEvent(city.Id, command.UnitType)]);
    }
}
