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
        if (ActingUnit(ruleset, view, unitId) is not { MovementLeft: > 0, DealId: null, Job: null } unit)
        {
            return [];
        }

        return [.. ReachabilityCalculator.Compute(unit, (from, to) => StepCost(ruleset, view, unit, from, to)).Destinations
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static DestinationPreview? DestinationPath(Ruleset ruleset, PlayerView view, UnitId unitId, HexCoord target)
    {
        if (ActingUnit(ruleset, view, unitId) is not { DealId: null, Job: null } unit || !view.Contains(target) || target == unit.Position
            || !ruleset.Unit(unit.Type).SharesTiles && view.HolderAt(ruleset, target) is not null)
        {
            return null;
        }

        var movement = ruleset.Unit(unit.Type).Movement;
        return PathFinder.Find(unit.Position, target, movement, (from, to) => KnownStepCost(ruleset, view, unit, from, to)) is { } path
            ? new DestinationPreview([.. path.Select(static step => step.Tile)], PathFinder.Turns(path, unit.MovementLeft, movement))
            : null;
    }

    public static ImmutableArray<HexCoord> AttackTargets(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (ActingUnit(ruleset, view, unitId) is not { } unit)
        {
            return [];
        }

        return [.. unit.Position.WithinRadius(ruleset.Unit(unit.Type).AttackRange)
            .Where(target => AttackRule.Check(ruleset, unit, target, TargetOf(ruleset, view, unit.Position, target)) is null)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static CombatForecast? CombatForecast(Ruleset ruleset, PlayerView view, UnitId unitId, HexCoord target)
    {
        if (ActingUnit(ruleset, view, unitId) is not { } unit
            || TargetOf(ruleset, view, unit.Position, target) is var targetInfo && AttackRule.Check(ruleset, unit, target, targetInfo) is not null
            || AttackRule.DefenderAt(ruleset, view.UnitsAt(target), view.CityAt(target) is not null, unit.Owner) is not { } defender)
        {
            return null;
        }

        // Both tiles are in sight (see AttackRule.Check), so their terrain and the river between them are known.
        var attacker = new Combatant(unit.Type, unit.Health(ruleset), view.TileAt(unit.Position).Terrain!.Value);
        var defending = new Combatant(defender.Type, defender.Health, view.TileAt(target).Terrain!.Value);
        return CombatCalculator.Forecast(ruleset, attacker, defending, targetInfo.River);
    }

    private static AttackTarget TargetOf(Ruleset ruleset, PlayerView view, HexCoord attacker, HexCoord target)
    {
        var city = view.Contains(target) ? view.CityAt(target) : null;
        var defender = view.Contains(target) ? AttackRule.DefenderAt(ruleset, view.UnitsAt(target), city is not null, view.Viewer) : null;
        var owner = defender?.Owner ?? city?.Owner;
        var domain = defender is null ? UnitDomain.Land : ruleset.Unit(defender.Type).Domain;
        return new AttackTarget(view.Contains(target), owner, defender is not null, view.CrossedRiverBetween(attacker, target), domain);
    }

    // The viewer always sees their own cities, so the name check gives the rule's answer.
    public static RuleViolation? CheckFoundCity(Ruleset ruleset, PlayerView view, UnitId unitId, string? name)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (OwnUnit(ruleset, view, unitId) is not { } settler)
        {
            return RuleViolations.UnitNotFound;
        }

        if (FoundCityRule.Check(ruleset, settler, view.Cities.Select(static city => city.Position), view.TileAt(settler.Position).TerritoryOwner is not null) is { } placement)
        {
            return placement;
        }

        return name is null
            ? null
            : FoundCityRule.CheckName(name, view.Cities.Where(city => city.Owner == view.Viewer).Select(static city => city.Name));
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

        // The unit's own tile is visible, with its territory and building, and every one of the viewer's cities is in the view with
        // its reserved site (only an own city can reserve a tile of own territory).
        var tile = view.TileAt(unit.Position);
        var inTerritory = tile.TerritoryCity is not null && view.CityAt(unit.Position) is null;
        var hasBuilding = tile.Building is not null || view.Cities.Any(city => city.ProducingBuildingSite == unit.Position);
        return BuildImprovementRule.Check(ruleset, unit, improvement, new ImprovementSite(tile.Terrain!.Value, tile.Improvement, inTerritory, hasBuilding));
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

    public static ImmutableArray<ResourceAmount> CityIncome(Ruleset ruleset, PlayerView view, CityId cityId)
    {
        if (view.Cities.FirstOrDefault(city => city.Id == cityId && city.Owner == view.Viewer) is not { } city)
        {
            return [];
        }

        // Every tile of the viewer's own territory is visible, with its terrain and improvement, and every neighbour is at least
        // explored, so the rivers on the territory's outer edges are known too.
        var tileYields = Enumerable.Range(0, view.Tiles.Length)
            .Where(index => view.Tiles[index].TerritoryCity == cityId)
            .SelectMany(index =>
            {
                var tile = view.Tiles[index];
                return YieldCalculator.TileYields(ruleset, tile.Terrain!.Value, view.RiversAround(view.CoordAt(index)), tile.Improvement);
            });
        return YieldCalculator.CityIncome(ruleset, tileYields, city.WorkingBuildings(ruleset.Upkeep.BuildingHealth));
    }

    // The city check, then, when no tile of the territory takes the building, the reason of the tile that came closest (the
    // city tile is skipped, since it never takes one): a harbour with free tiles but no lake says it needs the lake.
    public static RuleViolation? CheckProduceBuilding(Ruleset ruleset, PlayerView view, CityId cityId, BuildingType building)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (OwnCity(view, cityId) is not { } city)
        {
            return RuleViolations.CityNotFound;
        }

        if (ProduceBuildingRule.Check(ruleset, view.Science?.NodeList ?? [], city, building) is { } cityViolation)
        {
            return cityViolation;
        }

        var reasons = TerritoryOf(view, city)
            .Where(coord => coord != city.Position)
            .Select(coord => ProduceBuildingRule.CheckSite(ruleset.Building(building), BuildingSiteOf(ruleset, view, city, coord)))
            .ToList();
        return reasons.Contains(null) ? null : reasons.MaxBy(static reason => ProduceBuildingRule.SiteCheckOrder.IndexOf(reason!)) ?? RuleViolations.BuildingNotInTerritory;
    }

    public static ImmutableArray<HexCoord> BuildingSites(Ruleset ruleset, PlayerView view, CityId cityId, BuildingType building)
    {
        if (CheckTurn(view) is not null || OwnCity(view, cityId) is not { } city || ProduceBuildingRule.Check(ruleset, view.Science?.NodeList ?? [], city, building) is not null)
        {
            return [];
        }

        return [.. TerritoryOf(view, city)
            .Where(coord => ProduceBuildingRule.CheckSite(ruleset.Building(building), BuildingSiteOf(ruleset, view, city, coord)) is null)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    // The viewer's own city as the rules' checks take it. Its production is always in the view: only an enemy's is hidden.
    private static City? OwnCity(PlayerView view, CityId cityId) =>
        view.Cities.FirstOrDefault(city => city.Id == cityId && city.Owner == view.Viewer) is { Producing: { } producing, ProductionProgress: { } progress } own
            ? new City(own.Id, own.Owner, own.Name, own.Position, producing, progress, own.ProducingBuilding, own.Buildings, ProducingBuildingSite: own.ProducingBuildingSite)
            : null;

    private static IEnumerable<HexCoord> TerritoryOf(PlayerView view, City city) =>
        Enumerable.Range(0, view.Tiles.Length).Where(index => view.Tiles[index].TerritoryCity == city.Id).Select(view.CoordAt);

    // The viewer's own territory is always visible, with its improvements, buildings and units (with their jobs), and every
    // neighbour of it is at least explored, so the terrain next to a site is known; a site outside the territory is rejected first.
    private static BuildingSite BuildingSiteOf(Ruleset ruleset, PlayerView view, City city, HexCoord coord)
    {
        if (!view.Contains(coord) || view.TileAt(coord) is not { Terrain: { } terrain } tile)
        {
            return BuildingSite.Outside;
        }

        var improving = view.UnitsAt(coord).Any(unit => unit is { Job.Improvement: not null } && unit.Owner == city.Owner);
        var neighbors = coord.Neighbors().Where(view.Contains).Select(neighbor => view.TileAt(neighbor).Terrain).OfType<TerrainType>();
        return new BuildingSite(
            tile.TerritoryCity == city.Id && coord != city.Position,
            ruleset.Terrain(terrain).IsPassable,
            tile.Improvement is not null || tile.Building is not null,
            improving,
            [.. neighbors]);
    }

    // Both tiles are in sight (the target is a neighbour), and the view shows the territory of every visible tile.
    private static BridgeSite BridgeSiteOf(Ruleset ruleset, PlayerView view, Unit unit, HexCoord target)
    {
        if (HexEdge.Between(unit.Position, target) is null || !view.Contains(target))
        {
            return new BridgeSite(false, null, false, false, false);
        }

        var touchesEnemy = IsEnemyTerritory(view, unit.Position) || IsEnemyTerritory(view, target);
        var passable = view.TileAt(target).Terrain is { } terrain && ruleset.Terrain(terrain).IsPassable;
        return new BridgeSite(true, view.RiverBetween(unit.Position, target), view.BridgeBetween(unit.Position, target), passable, touchesEnemy);
    }

    private static bool IsEnemyTerritory(PlayerView view, HexCoord coord) =>
        view.TileAt(coord).TerritoryOwner is { } owner && owner != view.Viewer;

    // Only visible tiles can be entered. That loses nothing, because a unit's sight is at least its movement (checked by
    // RulesetValidator), so every tile it could reach is visible and shows every unit and city on it. Both ends of a step are
    // then visible, so the view knows the river between them. A trader minds only the ground and the river.
    private static int? StepCost(Ruleset ruleset, PlayerView view, Unit unit, HexCoord from, HexCoord to) =>
        view.Contains(to)
        && view.TileAt(to) is { Visibility: TileVisibility.Visible, Terrain: { } terrain }
        && IsFree(ruleset, view, unit, to)
            ? ReachabilityCalculator.StepCost(ruleset, ruleset.Unit(unit.Type).Domain, terrain, view.CrossedRiverBetween(from, to))
            : null;

    // A step as the viewer knows it, for a walk to a destination: onto any explored tile of the unit's domain, not onto a unit holding
    // a tile or an enemy city they see (a trader minds neither). The view shows rivers on explored tiles and bridges only on visible
    // ones, as AutoMoveRule plans on the state.
    internal static int? KnownStepCost(Ruleset ruleset, PlayerView view, Unit unit, HexCoord from, HexCoord to) =>
        view.Contains(to)
        && view.TileAt(to) is { Terrain: { } terrain } tile
        && (tile.Visibility != TileVisibility.Visible || IsFree(ruleset, view, unit, to))
            ? ReachabilityCalculator.StepCost(ruleset, ruleset.Unit(unit.Type).Domain, terrain, view.CrossedRiverBetween(from, to))
            : null;

    // No unit the viewer sees holds the tile and it is no enemy city, or the unit is a trader (it shares tiles).
    private static bool IsFree(Ruleset ruleset, PlayerView view, Unit unit, HexCoord to) =>
        ruleset.Unit(unit.Type).SharesTiles
        || (view.HolderAt(ruleset, to) is null && (view.CityAt(to) is not { } city || city.Owner == unit.Owner));

    // The viewer's unit when they may act with it now; otherwise null.
    internal static Unit? ActingUnit(Ruleset ruleset, PlayerView view, UnitId unitId) =>
        CheckTurn(view) is null ? OwnUnit(ruleset, view, unitId) : null;

    // The viewer's own unit as the rules' checks take it. Its movement is always in the view: only enemy movement is hidden.
    internal static Unit? OwnUnit(Ruleset ruleset, PlayerView view, UnitId unitId) =>
        view.FindOwnUnit(unitId) is { MovementLeft: { } movementLeft } own
            ? new Unit(own.Id, own.Owner, own.Type, own.Position, movementLeft, ruleset.Combat.MaxHealth - own.Health, own.Job, own.Destination, own.DealId, own.Exploring, own.HomeCity)
            : null;

    internal static RuleViolation? CheckTurn(PlayerView view) => CommandGuardRule.CheckTurn(
        view.Status,
        view.Players.FirstOrDefault(player => player.Id == view.Viewer)?.Status,
        view.MayAct,
        view.TurnMode);
}
