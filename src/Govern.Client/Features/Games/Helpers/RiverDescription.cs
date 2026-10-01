using System.Globalization;
using Govern.Core.Map;
using Govern.Core.Rulesets;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>A river kind's rules as one sentence, for the board's river titles.</summary>
public static class RiverDescription
{
    /// <summary>Describes crossing a river kind and the defence it gives.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="river">A river kind of that ruleset.</param>
    /// <returns>For example "River: +1 movement to cross, +3 defence against melee attacks across it".</returns>
    public static string For(Ruleset ruleset, RiverType river)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var definition = ruleset.River(river);
        var crossing = !definition.IsCrossable ? "land units can't cross"
            : definition.CrossingCost > 0 ? string.Create(CultureInfo.InvariantCulture, $"+{definition.CrossingCost} movement to cross")
            : "free to cross";
        var defence = definition.DefenceBonus > 0
            ? string.Create(CultureInfo.InvariantCulture, $", +{definition.DefenceBonus} defence against melee attacks across it")
            : "";
        return $"{ruleset.RiverName(river)}: {crossing}{defence}";
    }
}
