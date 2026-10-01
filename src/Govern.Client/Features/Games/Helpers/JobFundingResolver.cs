using Govern.Core.Cities;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Works out, from the player's view, which of their cities lends its production to which of their units' jobs at the end of
/// the turn, the way Core does it: jobs oldest first (the round they started, then the unit id), each fed by the own city
/// whose ground the unit stands on, and each city feeds one job a turn. Own units' jobs and own cities are all in the view.
/// </summary>
public static class JobFundingResolver
{
    /// <summary>The city each own job is fed by; jobs no city feeds are left out.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The player's view.</param>
    /// <returns>The feeding city per unit id.</returns>
    public static IReadOnlyDictionary<UnitId, CityView> FeedingCities(Ruleset ruleset, PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);

        var used = new HashSet<CityId>();
        var fed = new Dictionary<UnitId, CityView>();
        var working = view.Units
            .Where(unit => unit.Owner == view.Viewer && unit.Job is not null)
            .OrderBy(static unit => unit.Job!.StartedTurn)
            .ThenBy(static unit => unit.Id.Value);
        foreach (var unit in working)
        {
            var city = view.TileAt(unit.Position).TerritoryCity is { } territory ? view.Cities.FirstOrDefault(city => city.Id == territory) : null;
            if (city is not null && used.Add(city.Id))
            {
                fed[unit.Id] = city;
            }
        }

        return fed;
    }
}
