using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// Whether the viewer can unlock a science node or upgrade a unit or a building now, worked out from their
/// <see cref="PlayerView"/> alone (for the client). Each answer equals what <see cref="GameEngine.Execute"/> decides, because the
/// rules share their checks with it: the viewer's science, their own units and cities, and the tiles of their own territory are
/// all in the view.
/// </summary>
public static class SciencePreviewCalculator
{
    /// <summary>Whether the viewer can unlock a science node now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="node">The node's id.</param>
    /// <returns><see langword="null"/> when an <see cref="UnlockNodeCommand"/> would be accepted; otherwise its violation.</returns>
    public static RuleViolation? CheckUnlockNode(Ruleset ruleset, PlayerView view, string node)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (ViewPreviewCalculator.CheckTurn(view) is { } violation)
        {
            return violation;
        }

        var science = view.Science ?? new ScienceView(0);
        return ScienceCalculator.CheckUnlock(ruleset, science.NodeList, science.Income, node);
    }

    /// <summary>Whether one of the viewer's units can be upgraded now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The unit.</param>
    /// <returns><see langword="null"/> when an <see cref="UpgradeUnitCommand"/> would be accepted; otherwise its violation.</returns>
    public static RuleViolation? CheckUpgradeUnit(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (ViewPreviewCalculator.CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (ViewPreviewCalculator.OwnUnit(ruleset, view, unitId) is not { } unit)
        {
            return RuleViolations.UnitNotFound;
        }

        var city = view.TileAt(unit.Position).TerritoryCity is { } territory ? view.Cities.FirstOrDefault(own => own.Id == territory) : null;
        return UpgradeUnitRule.Check(ruleset, view.Science?.NodeList ?? [], unit, city?.StockList);
    }

    /// <summary>Whether one of the viewer's builders can start upgrading the building on its tile now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The builder.</param>
    /// <returns><see langword="null"/> when an <see cref="UpgradeBuildingCommand"/> would be accepted; otherwise its violation.</returns>
    public static RuleViolation? CheckUpgradeBuilding(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (ViewPreviewCalculator.CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (ViewPreviewCalculator.OwnUnit(ruleset, view, unitId) is not { } unit)
        {
            return RuleViolations.UnitNotFound;
        }

        var tile = view.TileAt(unit.Position);
        return UpgradeBuildingRule.Check(ruleset, view.Science?.NodeList ?? [], unit, tile.Building, tile.TerritoryCity is not null);
    }
}
