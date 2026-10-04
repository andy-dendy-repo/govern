using Govern.Core.Engine;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>
/// The action bar's Explore check: a search over the whole known map, so it runs once per view and unit, not on every render.
/// </summary>
public partial class ActionBar
{
    private (long Version, UnitId Unit, RuleViolation? Problem)? _explore;

    // Why Explore is refused for the unit in the current view, or null when it may go.
    private RuleViolation? ExploreProblem(UnitId unit)
    {
        if (_explore is not { } last || last.Version != View.Version || last.Unit != unit)
        {
            _explore = last = (View.Version, unit, GameEngine.CheckExplore(Ruleset, View, unit));
        }

        return last.Problem;
    }
}
