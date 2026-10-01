using System.Collections.Immutable;
using System.Globalization;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Client.Helpers;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Lenses;

/// <summary>
/// Shows what each known tile gives every turn (its terrain, a river on any of its edges, and its improvement when in sight): the
/// total as the label, tinted rich, poor or barren, and every resource in the tooltip. Where a city's territory reaches, its city
/// gathers it; elsewhere it is what the land offers an army.
/// </summary>
/// <param name="rulesets">The ruleset the yields come from.</param>
public sealed class YieldLens(RulesetService rulesets) : IMapLens
{
    // A tile giving at least this much in total is marked rich.
    private const int RichTotal = 4;

    /// <inheritdoc/>
    public string Id => "yields";

    /// <inheritdoc/>
    public string Name => "Yields";

    /// <inheritdoc/>
    public string Icon => Icons.Resource(new ResourceType("food"));

    /// <inheritdoc/>
    public LensMark? MarkFor(TileView tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Terrain is { } terrain ? Mark(terrain, YieldCalculator.TileYields(rulesets.Ruleset, terrain, [], tile.Improvement)) : null;
    }

    /// <inheritdoc/>
    public LensMark? MarkFor(PlayerView view, int index)
    {
        ArgumentNullException.ThrowIfNull(view);
        var tile = view.Tiles[index];
        if (tile.Terrain is not { } terrain)
        {
            return null;
        }

        var rivers = view.RiversAround(view.CoordAt(index));
        return Mark(terrain, YieldCalculator.TileYields(rulesets.Ruleset, terrain, rivers, tile.Improvement));
    }

    private LensMark Mark(TerrainType terrain, ImmutableArray<ResourceAmount> yields)
    {
        var ruleset = rulesets.Ruleset;
        var total = yields.Sum(static amount => amount.Amount);
        var name = ruleset.TerrainName(terrain);
        var description = yields.IsEmpty
            ? $"{name}: gives nothing"
            : $"{name}: {string.Join(", ", yields.Select(amount => $"{amount.Amount} {ruleset.ResourceName(amount.Resource).ToLowerInvariant()}"))} a turn";
        var richness = total switch
        {
            0 => "barren",
            >= RichTotal => "rich",
            _ => "poor",
        };
        return new LensMark(total.ToString(CultureInfo.InvariantCulture), richness, description);
    }
}
