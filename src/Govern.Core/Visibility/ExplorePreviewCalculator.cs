using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// Whether the viewer may set a unit exploring, worked out from their <see cref="PlayerView"/> alone. The view holds exactly the
/// owner's knowledge (explored tiles and those in sight), which is what <see cref="Engine.AutoMoveRule"/> explores over, so the
/// answer equals what <see cref="GameEngine.Execute"/> would decide.
/// </summary>
internal static class ExplorePreviewCalculator
{
    public static RuleViolation? CheckExplore(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (ViewPreviewCalculator.CheckTurn(view) is { } violation)
        {
            return violation;
        }

        if (ViewPreviewCalculator.OwnUnit(ruleset, view, unitId) is not { } unit)
        {
            return RuleViolations.UnitNotFound;
        }

        if (unit.RouteId is not null)
        {
            return RuleViolations.OnTradeRoute;
        }

        if (unit.Job is not null)
        {
            return RuleViolations.UnitBusy;
        }

        var nearest = ExploreFinder.Nearest(
            unit.Position,
            ruleset.Unit(unit.Type).Movement,
            (from, to) => ViewPreviewCalculator.KnownStepCost(ruleset, view, unit, from, to),
            coord => coord.Neighbors().Any(next => view.Contains(next) && view.TileAt(next).Visibility == TileVisibility.Unexplored));
        return nearest is null ? RuleViolations.NothingToExplore : null;
    }
}
