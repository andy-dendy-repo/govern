using Govern.Core.Economy;
using Govern.Core.Cities;
using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// What the viewer may do next, worked out from their <see cref="PlayerView"/> alone, for the client to preview. Each answer
/// equals what <see cref="GameEngine.Execute"/> would decide on the full state, because the rules share their checks with it
/// and the ruleset validator keeps sight at least movement, attack range and city spacing (<c>GameEnginePreviewTests</c> checks the parity).
/// </summary>
internal static class ViewPreviewCalculator
{
    public static ImmutableArray<HexCoord> ReachableTiles(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (ActingUnit(ruleset, view, unitId) is not { MovementLeft: > 0 } unit)
        {
            return [];
        }

        return [.. ReachabilityCalculator.Compute(unit, (from, to) => StepCost(ruleset, view, unit, from, to)).Destinations
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static ImmutableArray<HexCoord> AttackTargets(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (ActingUnit(ruleset, view, unitId) is not { } unit)
        {
            return [];
        }

        return [.. unit.Position.WithinRadius(ruleset.Unit(unit.Type).AttackRange)
            .Where(target => AttackRule.Check(ruleset, unit, target, TargetOf(view, unit.Position, target)) is null)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static CombatForecast? CombatForecast(Ruleset ruleset, PlayerView view, UnitId unitId, HexCoord target)
    {
        if (ActingUnit(ruleset, view, unitId) is not { } unit
            || TargetOf(view, unit.Position, target) is var targetInfo && AttackRule.Check(ruleset, unit, target, targetInfo) is not null
            || view.UnitAt(target) is not { } defender)
        {
            return null;
        }

        // Both tiles are in sight (see AttackRule.Check), so their terrain and the river between them are known.
        var attacker = new Combatant(unit.Type, unit.Health(ruleset), view.TileAt(unit.Position).Terrain!.Value);
        var defending = new Combatant(defender.Type, defender.Health, view.TileAt(target).Terrain!.Value);
        return CombatCalculator.Forecast(ruleset, attacker, defending, targetInfo.River);
    }

    private static AttackTarget TargetOf(PlayerView view, HexCoord attacker, HexCoord target)
    {
        var defender = view.Contains(target) ? view.UnitAt(target) : null;
        var owner = defender?.Owner ?? (view.Contains(target) ? view.CityAt(target)?.Owner : null);
        return new AttackTarget(view.Contains(target), owner, defender is not null, view.CrossedRiverBetween(attacker, target));
    }

    public static RuleViolation? CheckFoundCity(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        return OwnUnit(ruleset, view, unitId) is { } settler
            ? FoundCityRule.Check(ruleset, settler, view.Cities.Select(static city => city.Position))
            : RuleViolations.UnitNotFound;
    }

    public static RuleViolation? CheckBuildImprovement(Ruleset ruleset, PlayerView view, UnitId unitId, ImprovementType improvement)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (OwnUnit(ruleset, view, unitId) is not { } unit)
        {
            return RuleViolations.UnitNotFound;
        }

        // The unit's own tile is visible, and every one of the viewer's cities is in the view.
        var tile = view.TileAt(unit.Position);
        var inWorkArea = view.Cities.Any(city => city.Owner == view.Viewer && WorkArea.Contains(ruleset, city.Position, unit.Position));
        return BuildImprovementRule.Check(ruleset, unit, improvement, new ImprovementSite(tile.Terrain!.Value, tile.Improvement, inWorkArea));
    }

    public static ImmutableArray<HexCoord> BridgeTargets(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (ActingUnit(ruleset, view, unitId) is not { } unit)
        {
            return [];
        }

        return [.. unit.Position.Neighbors()
            .Where(target => BuildBridgeRule.Check(ruleset, unit, target, BridgeSiteOf(ruleset, view, unit, target)) is null)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static RuleViolation? CheckProduceBuilding(Ruleset ruleset, PlayerView view, CityId cityId, BuildingType building)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (view.Cities.FirstOrDefault(city => city.Id == cityId && city.Owner == view.Viewer) is not { Producing: { } producing, ProductionProgress: { } progress } own)
        {
            return RuleViolations.CityNotFound;
        }

        // A city sees its neighbours (city sight is at least 1), so their terrain is known.
        var city = new City(own.Id, own.Owner, own.Name, own.Position, producing, progress, own.ProducingBuilding, own.Buildings);
        var neighbors = own.Position.Neighbors()
            .Where(view.Contains)
            .Select(coord => view.TileAt(coord).Terrain)
            .OfType<TerrainType>();
        return ProduceBuildingRule.Check(ruleset, city, building, [.. neighbors]);
    }

    // Both tiles are in sight (the target is a neighbour), and so is every city whose work area could cover either of them: the
    // validator keeps a bridge builder's sight above the work radius.
    private static BridgeSite BridgeSiteOf(Ruleset ruleset, PlayerView view, Unit unit, HexCoord target)
    {
        if (HexEdge.Between(unit.Position, target) is null || !view.Contains(target))
        {
            return new BridgeSite(false, null, false, false, false);
        }

        var touchesEnemy = view.Cities.Any(city => city.Owner != unit.Owner
            && (WorkArea.Covers(ruleset, city.Position, unit.Position) || WorkArea.Covers(ruleset, city.Position, target)));
        var passable = view.TileAt(target).Terrain is { } terrain && ruleset.Terrain(terrain).IsPassable;
        return new BridgeSite(true, view.RiverBetween(unit.Position, target), view.BridgeBetween(unit.Position, target), passable, touchesEnemy);
    }

    // Only visible tiles can be entered. That loses nothing, because a unit's sight is at least its movement (checked by
    // RulesetValidator), so every tile it could reach is visible and shows every unit and city on it. Both ends of a step are
    // then visible, so the view knows the river between them.
    private static int? StepCost(Ruleset ruleset, PlayerView view, Unit unit, HexCoord from, HexCoord to) =>
        view.Contains(to)
        && view.TileAt(to) is { Visibility: TileVisibility.Visible, Terrain: { } terrain }
        && ruleset.Terrain(terrain).IsPassable
        && view.UnitAt(to) is null
        && (view.CityAt(to) is not { } city || city.Owner == unit.Owner)
            ? ReachabilityCalculator.StepCost(ruleset, terrain, view.CrossedRiverBetween(from, to))
            : null;

    // The viewer's unit when they may act with it now; otherwise null.
    private static Unit? ActingUnit(Ruleset ruleset, PlayerView view, UnitId unitId) =>
        CheckTurn(view) is null ? OwnUnit(ruleset, view, unitId) : null;

    // The viewer's own unit as the rules' checks take it. Its movement is always in the view: only enemy movement is hidden.
    private static Unit? OwnUnit(Ruleset ruleset, PlayerView view, UnitId unitId) =>
        view.FindOwnUnit(unitId) is { MovementLeft: { } movementLeft } own
            ? new Unit(own.Id, own.Owner, own.Type, own.Position, movementLeft, ruleset.Combat.MaxHealth - own.Health, own.Job)
            : null;

    private static RuleViolation? CheckTurn(PlayerView view) => CommandGuardRule.CheckTurn(
        view.Status,
        view.Players.FirstOrDefault(player => player.Id == view.Viewer)?.Status,
        view.CurrentPlayer == view.Viewer);
}
