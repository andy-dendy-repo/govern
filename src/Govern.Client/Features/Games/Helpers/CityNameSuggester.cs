namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Picks the name the found-city prompt offers: one of the ruleset's suggestions the player hasn't given a city yet, chosen at
/// random in the browser. The pick is random rather than the next in order, so a city's name never tells the opponent how many
/// cities its founder has.
/// </summary>
public static class CityNameSuggester
{
    /// <summary>Picks a suggestion.</summary>
    /// <param name="suggestions">The ruleset's suggested names (<c>Ruleset.SuggestedCityNames</c>).</param>
    /// <param name="ownCityNames">The names of the player's cities.</param>
    /// <param name="current">The name the prompt shows now; it is skipped while another name is free.</param>
    /// <param name="roll">A random number in [0, 1).</param>
    /// <returns>The name, or an empty text when the player already uses every suggestion.</returns>
    public static string Suggest(IReadOnlyList<string> suggestions, IEnumerable<string> ownCityNames, string? current, double roll)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(ownCityNames);

        var used = ownCityNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var free = suggestions.Where(name => !used.Contains(name)).ToList();
        if (free.Count > 1 && current is not null)
        {
            free.RemoveAll(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
        }

        return free.Count == 0 ? string.Empty : free[Math.Clamp((int)(roll * free.Count), 0, free.Count - 1)];
    }
}
