using System.Globalization;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Client.Helpers;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Lenses;

/// <summary>
/// Shows what each known tile costs to enter (from the terrain's <c>movementCost</c>), and ✕ where land units can't go.
/// </summary>
/// <param name="rulesets">The game's ruleset.</param>
public sealed class MovementCostLens(RulesetService rulesets) : IMapLens
{
    /// <inheritdoc />
    public string Id => "movement-cost";

    /// <inheritdoc />
    public string Name => "Movement cost";

    /// <inheritdoc />
    public string Icon => Icons.Movement;

    /// <inheritdoc />
    public LensMark? MarkFor(TileView tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (tile.Terrain is not { } terrain)
        {
            return null;
        }

        var definition = rulesets.Ruleset.Terrain(terrain);
        var name = rulesets.Ruleset.TerrainName(terrain);
        return definition.IsPassable
            ? new LensMark(
                definition.MovementCost.ToString(CultureInfo.InvariantCulture),
                definition.MovementCost > 1 ? "costly" : "cheap",
                $"{name}: costs {definition.MovementCost} movement to enter")
            : new LensMark("✕", "blocked", $"{name}: land units can't enter");
    }
}
