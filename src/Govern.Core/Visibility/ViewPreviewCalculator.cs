using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// What the viewer may do next, worked out from their <see cref="PlayerView"/> alone, for the client to preview. Each answer
/// equals what <see cref="GameEngine.Execute"/> would decide on the full state, because the rules share their checks with it
/// and the unit catalog keeps sight at least movement and city spacing (<c>GameEnginePreviewTests</c> checks the parity).
/// </summary>
internal static class ViewPreviewCalculator
{
    public static ImmutableArray<HexCoord> ReachableTiles(PlayerView view, UnitId unitId)
    {
        if (ActingUnit(view, unitId) is not { MovementLeft: > 0 } unit)
        {
            return [];
        }

        return [.. ReachabilityCalculator.Compute(unit, coord => CanEnter(view, unit, coord), coord => MovementCost(view, coord)).Destinations
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static ImmutableArray<HexCoord> AttackTargets(PlayerView view, UnitId unitId)
    {
        if (ActingUnit(view, unitId) is not { } unit)
        {
            return [];
        }

        return [.. unit.Position.Neighbors()
            .Where(target => AttackRule.Check(
                unit, target, view.Contains(target), view.UnitAt(target)?.Owner, view.CityAt(target)?.Owner) is null)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    public static RuleViolation? CheckFoundCity(PlayerView view, UnitId unitId)
    {
        if (CheckTurn(view) is { } violation)
        {
            return violation;
        }

        return view.FindOwnUnit(unitId) is { } settler
            ? FoundCityRule.Check(settler, view.Cities.Select(static city => city.Position))
            : RuleViolations.UnitNotFound;
    }

    // Only visible tiles can be entered. That loses nothing, because a unit's sight is at least its movement (UnitCatalogTests),
    // so every tile it could reach is visible and shows every unit and city on it.
    private static bool CanEnter(PlayerView view, Unit unit, HexCoord coord) =>
        view.Contains(coord)
        && view.TileAt(coord) is { Visibility: TileVisibility.Visible, Terrain: { } terrain }
        && TerrainCatalog.Get(terrain).IsPassable
        && view.UnitAt(coord) is null
        && (view.CityAt(coord) is not { } city || city.Owner == unit.Owner);

    private static int MovementCost(PlayerView view, HexCoord coord) =>
        TerrainCatalog.Get(view.TileAt(coord).Terrain!.Value).MovementCost;

    // The viewer's unit when they may act with it now; otherwise null.
    private static Unit? ActingUnit(PlayerView view, UnitId unitId) =>
        CheckTurn(view) is null ? view.FindOwnUnit(unitId) : null;

    private static RuleViolation? CheckTurn(PlayerView view) => CommandGuardRule.CheckTurn(
        view.Status,
        view.Players.FirstOrDefault(player => player.Id == view.Viewer)?.Status,
        view.CurrentPlayer == view.Viewer);
}
