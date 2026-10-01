using Govern.Core.Economy;
using Govern.Core.Rulesets;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>Writes resource amounts as text for the panels, such as "2 food, 1 water".</summary>
public static class ResourceFormatter
{
    /// <summary>Describes amounts in the order given, with lower-case resource names.</summary>
    /// <param name="ruleset">The game's ruleset (the names).</param>
    /// <param name="amounts">The amounts.</param>
    /// <returns>The amounts joined by commas; empty when there are none.</returns>
    public static string Describe(Ruleset ruleset, IEnumerable<ResourceAmount> amounts)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return string.Join(", ", amounts.Select(amount => $"{amount.Amount} {ruleset.ResourceName(amount.Resource).ToLowerInvariant()}"));
    }
}
