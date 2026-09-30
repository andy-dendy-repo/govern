namespace Govern.Core.Rulesets;

/// <summary>
/// Where players start (the ruleset's <c>map.spawn</c> section). The first player spawns in a zone at the left edge, the second
/// in a zone at the right edge.
/// </summary>
/// <param name="ZoneColumnsPerMille">The width of each spawn zone, in thousandths of the map width (250 = a quarter).</param>
/// <param name="EdgeMargin">Tiles between a spawn and the map edge.</param>
public sealed record SpawnRules(int ZoneColumnsPerMille, int EdgeMargin)
{
    /// <summary>The width of each spawn zone on a map.</summary>
    /// <param name="mapWidth">The map width.</param>
    /// <returns>The number of columns, rounded down.</returns>
    public int ZoneColumns(int mapWidth) => mapWidth * ZoneColumnsPerMille / 1000;
}
