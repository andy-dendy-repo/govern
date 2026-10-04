using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Starts upgrading the building on the builder's tile into its unlocked upgrade (G11a). The tile must be in the territory of
/// one of the actor's cities. The job works like an improvement's: it gains the builder's build production plus the city's
/// production each turn, and when it reaches the upgrade's cost the city pays the upgrade's resource cost and the new building
/// replaces the old one on its tile, at full health; while the city can't pay, the finished job waits.
/// </summary>
/// <param name="UnitId">The actor's builder.</param>
public sealed record UpgradeBuildingCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Gives a builder the job of upgrading the building on its tile (see <see cref="UnitJob.UpgradeTo"/>). Starting uses the unit's
/// remaining movement and replaces any job it had.
/// </summary>
internal static class UpgradeBuildingRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, UpgradeBuildingCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        var tile = state.Map.TileAt(unit.Position);
        var nodes = state.FindPlayer(actor)!.NodeList;
        if (Check(ruleset, nodes, unit, tile.Building, OwnCity(state, unit) is not null) is { } violation)
        {
            return violation;
        }

        var job = UnitJob.ForUpgrade(ScienceCalculator.BuildingUpgrade(ruleset, nodes, tile.Building!.Value)!.Id, state.Turn);
        return new GameUpdate(state.WithUnit(unit with { MovementLeft = 0, Job = job, Destination = null }), [new BuildStartedEvent(unit.Id, job)]);
    }

    // Shared with the view-based preview: the unit's own tile, its building and whether it is the viewer's territory are always
    // visible to the unit's owner.
    public static RuleViolation? Check(Ruleset ruleset, ImmutableArray<OwnedNode> nodes, Unit unit, BuildingType? building, bool inOwnTerritory)
    {
        if (!ruleset.Unit(unit.Type).CanBuildImprovements)
        {
            return RuleViolations.CannotBuildImprovements;
        }

        if (building is not { } standing || ruleset.UpgradeOf(standing) is not { } upgrade)
        {
            return RuleViolations.NoUpgrade;
        }

        if (!inOwnTerritory)
        {
            return RuleViolations.UpgradeNotInTerritory;
        }

        if (!ScienceCalculator.IsUnlocked(ruleset, nodes, upgrade.Id))
        {
            return RuleViolations.Locked;
        }

        if (unit.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        return unit.Job?.UpgradeTo == upgrade.Id ? RuleViolations.AlreadyBuilding : null;
    }

    // The actor's city whose territory holds the unit's tile, if any.
    public static City? OwnCity(GameState state, Unit unit) =>
        state.Map.TileAt(unit.Position).Territory is { } territory && state.FindCity(territory) is { } city && city.Owner == unit.Owner
            ? city
            : null;

    // Whether an upgrade job may go on: its building still stands on the unit's tile, in the territory of one of the unit owner's
    // cities. Checked again each turn while the job lasts.
    public static bool StillValid(Ruleset ruleset, GameState state, Unit unit, BuildingType upgrade) =>
        OwnCity(state, unit) is not null && state.Map.TileAt(unit.Position).Building == ruleset.Building(upgrade).Upgrades;

    // Finishes the upgrade when the city can pay: it pays, the building is replaced in the city's list (at full health) and on the
    // tile. Returns null when the city can't pay yet.
    public static GameState? Complete(Ruleset ruleset, GameState state, Unit unit, BuildingType upgrade, ImmutableArray<GameEvent>.Builder events)
    {
        var city = OwnCity(state, unit)!;
        var definition = ruleset.Building(upgrade);
        if (!UpkeepCalculator.Missing(ruleset, city.StockList, definition.ResourceCost).IsEmpty)
        {
            return null;
        }

        var old = definition.Upgrades!.Value;
        var stock = UpkeepCalculator.Subtract(ruleset, city.StockList, definition.ResourceCost);
        var damage = city.BuildingDamageList.RemoveAll(entry => entry.Building == old);
        var changed = state.WithUnit(unit with { Job = null }).WithCity(city with
        {
            Stock = stock.IsEmpty ? null : stock,
            Buildings = [.. city.BuildingList.Select(building => building == old ? upgrade : building)],
            BuildingDamage = damage.IsEmpty ? null : damage,
        });
        events.Add(new BuildingUpgradedEvent(unit.Id, unit.Owner, unit.Position, old, upgrade, city.Id));
        var tile = changed.Map.TileAt(unit.Position);
        return changed with { Map = changed.Map.WithTile(tile with { Building = upgrade }) };
    }
}
