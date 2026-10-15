namespace Govern.Core.Rulesets;

/// <summary>
/// Where players start on a generated map (the ruleset's <c>map.spawn</c> section). With two players the first spawns in a
/// zone at the left edge and the second at the right edge. With three, the first spawns at the left edge and the others in the
/// top and bottom thirds of the right edge; with four, each spawns in a corner (top left, top right, bottom right, bottom left).
/// </summary>
/// <param name="ZoneColumnsPerMille">The width of each spawn zone, in thousandths of the map width (250 = a quarter).</param>
/// <param name="EdgeMargin">Tiles between a spawn and the map edge.</param>
public sealed record SpawnRules(int ZoneColumnsPerMille, int EdgeMargin)
{
    /// <summary>The width of each spawn zone on a map.</summary>
    /// <param name="mapWidth">The map width.</param>
    /// <returns>The number of columns, rounded down.</returns>
    public int ZoneColumns(int mapWidth) => mapWidth * ZoneColumnsPerMille / 1000;

    /// <summary>The height of a corner spawn zone (three or four players) on a map.</summary>
    /// <param name="mapHeight">The map height.</param>
    /// <returns>A third of the height, rounded down.</returns>
    public static int CornerRows(int mapHeight) => mapHeight / 3;
}
