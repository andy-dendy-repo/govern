using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Starts building a bridge over the river between the unit's tile and a neighbouring tile.
/// </summary>
/// <param name="UnitId">The actor's unit that can build bridges.</param>
/// <param name="Target">The neighbouring tile across the river.</param>
public sealed record BuildBridgeCommand(UnitId UnitId, HexCoord Target) : GameCommand;

/// <summary>
/// Gives a unit the job of bridging the river between its tile and a neighbour (see <see cref="UnitJob"/>). The edge must have a
/// river and no bridge, the far side must be passable, and neither tile may be in an enemy city's territory. Starting
/// uses the unit's remaining movement and replaces any job it had.
/// </summary>
internal static class BuildBridgeRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, BuildBridgeCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (Check(ruleset, unit, command.Target, SiteOf(ruleset, state, unit, command.Target)) is { } violation)
        {
            return violation;
        }

        var job = UnitJob.ForBridge(command.Target, state.Turn);
        return new GameUpdate(state.WithUnit(unit with { MovementLeft = 0, Job = job }), [new BuildStartedEvent(unit.Id, job)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator): both tiles are in sight (the target is a neighbour), and the view
    // shows the territory of every visible tile.
    public static RuleViolation? Check(Ruleset ruleset, Unit unit, HexCoord target, BridgeSite site)
    {
        if (!ruleset.Unit(unit.Type).CanBuildBridges)
        {
            return RuleViolations.CannotBuildBridges;
        }

        if (unit.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        return unit.Job?.BridgeTo == target ? RuleViolations.AlreadyBuilding : CheckPlacement(site);
    }

    // Whether a bridge may be built on the edge; also checked again each turn while the job lasts.
    public static RuleViolation? CheckPlacement(BridgeSite site)
    {
        if (!site.IsNeighbor)
        {
            return RuleViolations.BridgeNotAdjacent;
        }

        if (site.River is null)
        {
            return RuleViolations.NoRiver;
        }

        if (site.Bridged)
        {
            return RuleViolations.BridgeExists;
        }

        if (!site.TargetPassable)
        {
            return RuleViolations.BridgeLeadsNowhere;
        }

        return site.TouchesEnemyTerritory ? RuleViolations.EnemyTerritory : null;
    }

    public static BridgeSite SiteOf(Ruleset ruleset, GameState state, Unit unit, HexCoord target)
    {
        var isNeighbor = HexEdge.Between(unit.Position, target) is not null && state.Map.Contains(target);
        if (!isNeighbor)
        {
            return new BridgeSite(false, null, false, false, false);
        }

        var touchesEnemy = IsEnemyTerritory(state, unit, unit.Position) || IsEnemyTerritory(state, unit, target);
        return new BridgeSite(
            true,
            state.Map.RiverBetween(unit.Position, target),
            state.Map.BridgeBetween(unit.Position, target),
            state.Map.IsPassable(ruleset, target),
            touchesEnemy);
    }

    private static bool IsEnemyTerritory(GameState state, Unit unit, HexCoord coord) =>
        state.Map.TileAt(coord).Territory is { } territory && state.FindCity(territory) is { } city && city.Owner != unit.Owner;
}
