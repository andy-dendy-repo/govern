using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Upgrades a unit into the newest version of its line the actor has unlocked (G11a), on a tile of one of the actor's cities'
/// territory. The city owning the tile pays the new unit's upgrade cost; the unit keeps its health and its tile, and loses its
/// remaining movement, its job and its destination.
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
public sealed record UpgradeUnitCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Turns a unit into its upgrade when it has movement left, a newer version of its line is unlocked, it stands on its owner's
/// territory and the city owning the tile holds the upgrade cost.
/// </summary>
internal static class UpgradeUnitRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, UpgradeUnitCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        var city = state.Map.TileAt(unit.Position).Territory is { } territory && state.FindCity(territory) is { } owner && owner.Owner == actor
            ? owner
            : null;
        var nodes = state.FindPlayer(actor)!.NodeList;
        if (Check(ruleset, nodes, unit, city?.StockList) is { } violation)
        {
            return violation;
        }

        var upgrade = ScienceCalculator.UnitUpgrade(ruleset, nodes, unit.Type)!;
        var stock = UpkeepCalculator.Subtract(ruleset, city!.StockList, upgrade.UpgradeCost ?? []);
        var upgraded = state
            .WithCity(city with { Stock = stock.IsEmpty ? null : stock })
            .WithUnit(unit with { Type = upgrade.Id, MovementLeft = 0, Job = null, Destination = null, Exploring = false });
        return new GameUpdate(upgraded, [new UnitUpgradedEvent(unit.Id, actor, unit.Position, unit.Type, upgrade.Id)]);
    }

    // Shared with the view-based preview: the unit's own tile is always visible, and so is the stock of the viewer's own city.
    // The stock is null when the tile isn't in one of the actor's cities' territory.
    public static RuleViolation? Check(Ruleset ruleset, ImmutableArray<OwnedNode> nodes, Unit unit, ImmutableArray<ResourceAmount>? stock)
    {
        if (unit.DealId is not null)
        {
            return RuleViolations.OnTradeDeal;
        }

        if (ScienceCalculator.UnitUpgrade(ruleset, nodes, unit.Type) is not { } upgrade)
        {
            return RuleViolations.NoUpgrade;
        }

        if (unit.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        if (stock is not { } held)
        {
            return RuleViolations.UpgradeNotInTerritory;
        }

        return UpkeepCalculator.Missing(ruleset, held, upgrade.UpgradeCost ?? []).IsEmpty ? null : RuleViolations.UpgradeMissingResources;
    }
}
