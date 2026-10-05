using Govern.Client.Features.Games.Models;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Decides what a click on a tile does, from the player's view alone. Moves and attacks come from the Core previews, which
/// accept exactly what the server would, so a command built here is rejected only when the game changed in the meantime.
/// </summary>
public static class BoardClickResolver
{
    /// <summary>Resolves a click.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The player's current view.</param>
    /// <param name="selection">The selection before the click.</param>
    /// <param name="coord">The clicked tile.</param>
    /// <returns>
    /// A move by the selected own unit when the tile allows it (never while it is not the player's turn or the game is over).
    /// On an enemy unit it can attack, a click selects it as the <see cref="BoardSelection.Target"/> (the forecast is shown);
    /// only the forecast's Attack button attacks, so a double-click never attacks unseen. An undefended enemy city is captured
    /// at once. In bridge mode, a click on a marked neighbour builds the bridge and any other click only leaves bridge mode.
    /// While the selected unit chooses a destination, a click on a tile with a known way there orders the walk and any other
    /// click only stops choosing.
    /// While the selected city places a building, a click on a marked tile orders the building there and any other click only
    /// stops placing.
    /// Otherwise the new selection: a unit on
    /// the tile (clicking again selects the next unit sharing it, then the player's city under them), else the city on the tile,
    /// else nothing.
    /// </returns>
    public static BoardClick Resolve(Ruleset ruleset, PlayerView view, BoardSelection selection, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        if (PickTile(ruleset, view, selection, coord) is { } picked)
        {
            return picked;
        }

        if (selection.Unit is { } unitId && view.FindOwnUnit(unitId) is not null && Act(ruleset, view, selection, unitId, coord) is { } acted)
        {
            return acted;
        }

        var units = UnitsOn(ruleset, view, coord);
        var city = view.CityAt(coord);
        if (units.Count > 0)
        {
            // Units sharing a tile (a trader with others) are selected in turn; after the last comes the player's city there.
            var index = selection.Unit is { } selected ? units.FindIndex(unit => unit.Id == selected) : -1;
            var next = index + 1 < units.Count ? BoardSelection.Of(units[index + 1].Id)
                : city is not null && city.Owner == view.Viewer ? BoardSelection.Of(city.Id)
                : BoardSelection.Of(units[0].Id);
            return new BoardClick(next, null);
        }

        return new BoardClick(city is null ? BoardSelection.None : BoardSelection.Of(city.Id), null);
    }

    // What a click does with the selected own unit: an enemy it can hit shows the forecast (the attack is the forecast's button);
    // a click on a tile it can also enter (a lone enemy trader, which blocks nothing) moves there once the forecast shows. Any
    // other tile it can reach is a move, an undefended enemy city it can reach is captured; null when the click does neither.
    private static BoardClick? Act(Ruleset ruleset, PlayerView view, BoardSelection selection, UnitId unitId, HexCoord coord)
    {
        var reachable = GameEngine.ReachableTiles(ruleset, view, unitId).Contains(coord);
        var attackable = GameEngine.AttackTargets(ruleset, view, unitId).Contains(coord);
        if (attackable && view.DefenderAt(ruleset, coord) is not null && !(reachable && selection.Target == coord))
        {
            return new BoardClick(selection with { Target = coord }, null);
        }

        if (reachable)
        {
            return new BoardClick(selection with { Target = null }, new MoveUnitCommand(unitId, coord));
        }

        return attackable ? new BoardClick(selection with { Target = null }, new AttackCommand(unitId, coord)) : null;
    }

    // The units on a tile in the order clicks select them: the player's own first, the unit holding the tile before traders.
    private static List<UnitView> UnitsOn(Ruleset ruleset, PlayerView view, HexCoord coord) => [.. view.UnitsAt(coord)
        .OrderBy(unit => unit.Owner == view.Viewer ? 0 : 1)
        .ThenBy(unit => ruleset.Unit(unit.Type).SharesTiles ? 1 : 0)
        .ThenBy(static unit => unit.Id.Value)];

    // A click while choosing a tile (bridge mode, a destination, or a city placing a building) ends the choice, and on a marked tile sends the
    // command; null when no tile is being chosen.
    private static BoardClick? PickTile(Ruleset ruleset, PlayerView view, BoardSelection selection, HexCoord coord)
    {
        if (selection is { Unit: { } builder, Bridge: true } && view.FindOwnUnit(builder) is not null)
        {
            return GameEngine.BridgeTargets(ruleset, view, builder).Contains(coord)
                ? new BoardClick(selection with { Bridge = false }, new BuildBridgeCommand(builder, coord))
                : new BoardClick(selection with { Bridge = false }, null);
        }

        if (selection is { Unit: { } walker, Destination: true } && view.FindOwnUnit(walker) is not null)
        {
            return GameEngine.DestinationPath(ruleset, view, walker, coord) is not null
                ? new BoardClick(selection with { Destination = false }, new SetDestinationCommand(walker, coord))
                : new BoardClick(selection with { Destination = false }, null);
        }

        if (selection is { City: { } city, Placing: { } building })
        {
            // The building's current site is offered again but changes nothing, so picking it only stops placing.
            var current = view.Cities.FirstOrDefault(own => own.Id == city) is { } shown && shown.ProducingBuilding == building && shown.ProducingBuildingSite == coord;
            return !current && GameEngine.BuildingSites(ruleset, view, city, building).Contains(coord)
                ? new BoardClick(selection with { Placing = null }, new ProduceBuildingCommand(city, building, coord))
                : new BoardClick(selection with { Placing = null }, null);
        }

        return null;
    }

    /// <summary>
    /// Keeps a selection valid for a new view: a unit or city that is no longer in it (destroyed, captured, or out of sight)
    /// is dropped, and so is a pending attack target the unit can no longer fight, bridge mode with nothing left to bridge,
    /// choosing a destination when the player can't act, and placing a building with no tile left to take it.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The new view.</param>
    /// <param name="selection">The current selection.</param>
    /// <returns>The selection, without a stale target, or <see cref="BoardSelection.None"/>.</returns>
    public static BoardSelection Prune(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection is { Unit: { } attacker, Target: { } target } && GameEngine.CombatForecast(ruleset, view, attacker, target) is null)
        {
            selection = selection with { Target = null };
        }

        if (selection is { Unit: { } builder, Bridge: true } && GameEngine.BridgeTargets(ruleset, view, builder).IsEmpty)
        {
            selection = selection with { Bridge = false };
        }

        if (selection.Destination && (view.CurrentPlayer != view.Viewer || view.Status != GameStatus.InProgress))
        {
            selection = selection with { Destination = false };
        }

        if (selection is { City: { } placer, Placing: { } building } && GameEngine.BuildingSites(ruleset, view, placer, building).IsEmpty)
        {
            selection = selection with { Placing = null };
        }

        var kept = selection switch
        {
            { Unit: { } unit } => view.Units.Any(candidate => candidate.Id == unit),
            { City: { } city } => view.Cities.Any(candidate => candidate.Id == city),
            _ => false,
        };
        return kept ? selection : BoardSelection.None;
    }

    /// <summary>
    /// The tiles to mark for the selection: where the selected own unit can move and what it can attack, or in bridge mode
    /// only the neighbours it can bridge to, and while it chooses a destination only its current route; the selected own unit's
    /// route to its destination; for a city placing a building, the tiles that can take it.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The player's current view.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>The marked tiles; none unless an own unit that can act now, or an own city placing a building, is selected.</returns>
    public static BoardHighlights Highlights(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        return selection switch
        {
            { Unit: { } builder, Bridge: true } => new BoardHighlights([], [], [.. GameEngine.BridgeTargets(ruleset, view, builder)], []),
            { City: { } city, Placing: { } building } => new BoardHighlights([], [], [], [.. GameEngine.BuildingSites(ruleset, view, city, building)]),
            { Unit: { } walker, Destination: true } => new BoardHighlights([], [], [], [], Route(ruleset, view, walker)),
            { Unit: { } unitId } => new BoardHighlights(
                [.. GameEngine.ReachableTiles(ruleset, view, unitId)], [.. GameEngine.AttackTargets(ruleset, view, unitId)], [], [], Route(ruleset, view, unitId)),
            _ => BoardHighlights.None,
        };
    }

    // The walk to the unit's current destination, if it has one and a known way there.
    private static DestinationPreview? Route(Ruleset ruleset, PlayerView view, UnitId unitId) =>
        view.FindOwnUnit(unitId)?.Destination is { } destination ? GameEngine.DestinationPath(ruleset, view, unitId, destination) : null;
}
