using Govern.Client.Features.Games.Models;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The board's look per ruleset river kind: wider and darker as the river gets deeper, so kinds differ by more than colour
/// (the design gallery's <c>.river-&lt;id&gt;</c> swatches follow the same order). Presentation
/// only; every ruleset river needs an entry (<c>RiverReliefsTests</c>). An unknown kind is drawn as a river.
/// </summary>
public static class RiverReliefs
{
    /// <summary>The relief drawn for a river kind that has no entry.</summary>
    public static readonly RiverRelief Default = new(0.17, false);

    private static readonly Dictionary<string, RiverRelief> ById = new(StringComparer.Ordinal)
    {
        ["stream"] = new(0.09, false),
        ["river"] = Default,
        ["deep_river"] = new(0.27, true),
    };

    /// <summary>The river ids that have an entry.</summary>
    public static IEnumerable<string> Known => ById.Keys;

    /// <summary>The relief of a river kind.</summary>
    /// <param name="river">The river kind.</param>
    /// <returns>Its relief, or <see cref="Default"/> when it has no entry.</returns>
    public static RiverRelief For(RiverType river) => ById.GetValueOrDefault(river.Id, Default);
}
