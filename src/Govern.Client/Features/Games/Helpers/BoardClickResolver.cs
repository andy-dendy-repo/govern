using Govern.Client.Features.Games.Models;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
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
    /// Otherwise the new selection: the unit on
    /// the tile (clicking a selected unit again selects the player's city under it), else the city on the tile, else nothing.
    /// </returns>
    public static BoardClick Resolve(Ruleset ruleset, PlayerView view, BoardSelection selection, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection is { Unit: { } builder, Bridge: true } && view.FindOwnUnit(builder) is not null)
        {
            return GameEngine.BridgeTargets(ruleset, view, builder).Contains(coord)
                ? new BoardClick(selection with { Bridge = false }, new BuildBridgeCommand(builder, coord))
                : new BoardClick(selection with { Bridge = false }, null);
        }

        if (selection.Unit is { } unitId && view.FindOwnUnit(unitId) is not null)
        {
            if (GameEngine.ReachableTiles(ruleset, view, unitId).Contains(coord))
            {
                return new BoardClick(selection with { Target = null }, new MoveUnitCommand(unitId, coord));
            }

            if (GameEngine.AttackTargets(ruleset, view, unitId).Contains(coord))
            {
                return view.UnitAt(coord) is null
                    ? new BoardClick(selection with { Target = null }, new AttackCommand(unitId, coord))
                    : new BoardClick(selection with { Target = coord }, null);
            }
        }

        var unit = view.UnitAt(coord);
        var city = view.CityAt(coord);
        if (unit is not null)
        {
            var toCity = selection.Unit == unit.Id && city is not null && city.Owner == view.Viewer;
            return new BoardClick(toCity ? BoardSelection.Of(city!.Id) : BoardSelection.Of(unit.Id), null);
        }

        return new BoardClick(city is null ? BoardSelection.None : BoardSelection.Of(city.Id), null);
    }

    /// <summary>
    /// Keeps a selection valid for a new view: a unit or city that is no longer in it (destroyed, captured, or out of sight)
    /// is dropped, and so is a pending attack target the unit can no longer fight and bridge mode with nothing left to bridge.
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
    /// only the neighbours it can bridge to.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The player's current view.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>The marked tiles; none unless an own unit that can act now is selected.</returns>
    public static BoardHighlights Highlights(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        return selection switch
        {
            { Unit: { } builder, Bridge: true } => new BoardHighlights([], [], [.. GameEngine.BridgeTargets(ruleset, view, builder)]),
            { Unit: { } unitId } => new BoardHighlights(
                [.. GameEngine.ReachableTiles(ruleset, view, unitId)], [.. GameEngine.AttackTargets(ruleset, view, unitId)], []),
            _ => BoardHighlights.None,
        };
    }
}
