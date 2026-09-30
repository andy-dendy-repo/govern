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
    /// A move or attack by the selected own unit when the tile allows it (never while it is not the player's turn or the game
    /// is over); otherwise the new selection: the unit on the tile (clicking a selected unit again selects the player's city
    /// under it), else the city on the tile, else nothing.
    /// </returns>
    public static BoardClick Resolve(Ruleset ruleset, PlayerView view, BoardSelection selection, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Unit is { } unitId && view.FindOwnUnit(unitId) is not null)
        {
            if (GameEngine.ReachableTiles(ruleset, view, unitId).Contains(coord))
            {
                return new BoardClick(selection, new MoveUnitCommand(unitId, coord));
            }

            if (GameEngine.AttackTargets(ruleset, view, unitId).Contains(coord))
            {
                return new BoardClick(selection, new AttackCommand(unitId, coord));
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
    /// is dropped.
    /// </summary>
    /// <param name="view">The new view.</param>
    /// <param name="selection">The current selection.</param>
    /// <returns>The selection, or <see cref="BoardSelection.None"/>.</returns>
    public static BoardSelection Prune(PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        var kept = selection switch
        {
            { Unit: { } unit } => view.Units.Any(candidate => candidate.Id == unit),
            { City: { } city } => view.Cities.Any(candidate => candidate.Id == city),
            _ => false,
        };
        return kept ? selection : BoardSelection.None;
    }

    /// <summary>The tiles to mark for the selection: where the selected own unit can move and what it can attack.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The player's current view.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>The marked tiles; none unless an own unit that can act now is selected.</returns>
    public static BoardHighlights Highlights(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);

        return selection.Unit is { } unitId
            ? new BoardHighlights([.. GameEngine.ReachableTiles(ruleset, view, unitId)], [.. GameEngine.AttackTargets(ruleset, view, unitId)])
            : BoardHighlights.None;
    }
}
