using System.Collections.Immutable;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

// The shape checks of a map document (PredefinedMapParser): the header, the legend, every row and river edge, the spawn
// candidates. It builds the map from them; the fairness checks on the built map are PredefinedMapBalanceValidator's. Every
// problem is collected with its JSON path, so an author sees them all at once.
internal static class PredefinedMapValidator
{
    private const int MaxNameLength = 40;

    // The candidates are public, so they narrow down where the opponent started; with one, the opponent's start would be known.
    private const int MinSpawns = 2;

    public static PredefinedMap? Validate(Ruleset ruleset, PredefinedMapDocument document, List<RulesetProblem> problems)
    {
        CheckHeader(ruleset, document, problems);
        var legend = ReadLegend(ruleset, document.Legend, problems);
        if (problems.Count > 0)
        {
            return null;
        }

        var map = ReadTiles(document, legend, problems);
        map = ReadRivers(ruleset, document, map, problems);
        var spawns = ReadSpawns(document, map, problems);
        if (problems.Count > 0)
        {
            return null;
        }

        PredefinedMapBalanceValidator.Validate(ruleset, map, spawns, problems);
        return problems.Count == 0 ? new PredefinedMap(document.Key, document.Name, map, spawns) : null;
    }

    private static void CheckHeader(Ruleset ruleset, PredefinedMapDocument document, List<RulesetProblem> problems)
    {
        if (!RulesetValidator.IsValidId(document.Key))
        {
            problems.Add(new("$.key", $"'{document.Key}' must be lower snake_case: a letter, then letters, digits or '_'."));
        }

        if (string.IsNullOrWhiteSpace(document.Name) || document.Name.Length > MaxNameLength || document.Name.Trim() != document.Name)
        {
            problems.Add(new("$.name", $"The name must be 1 to {MaxNameLength} characters, with no space at either end."));
        }

        var rules = ruleset.Map;
        foreach (var (path, size) in new[] { ("$.width", document.Width), ("$.height", document.Height) })
        {
            if (size < rules.MinSize || size > rules.MaxSize)
            {
                problems.Add(new(path, $"Must be from {rules.MinSize} to {rules.MaxSize}."));
            }
        }

        // GameMap.MirrorOf keeps neighbours neighbours only on an even height.
        if (document.Height % 2 != 0)
        {
            problems.Add(new("$.height", "Must be even, so the map's 180° turn keeps neighbours neighbours."));
        }
    }

    private static Dictionary<char, TerrainType> ReadLegend(Ruleset ruleset, ImmutableDictionary<string, TerrainType> legend, List<RulesetProblem> problems)
    {
        var terrains = new Dictionary<char, TerrainType>();
        foreach (var (symbol, terrain) in legend.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (symbol.Length != 1 || char.IsWhiteSpace(symbol[0]))
            {
                problems.Add(new($"$.legend['{symbol}']", "A legend key must be one character that is not a space."));
            }
            else if (!ruleset.Terrains.Any(definition => definition.Id == terrain))
            {
                problems.Add(new($"$.legend['{symbol}']", $"'{terrain.Id}' is not a terrain of the ruleset."));
            }
            else
            {
                terrains[symbol[0]] = terrain;
            }
        }

        if (legend.IsEmpty)
        {
            problems.Add(new("$.legend", "At least one entry is required."));
        }

        return terrains;
    }

    // Unknown characters and wrong row lengths are reported; the map is still built (with the first legend terrain in their
    // place) so the rivers and spawns can be checked too.
    private static GameMap ReadTiles(PredefinedMapDocument document, Dictionary<char, TerrainType> legend, List<RulesetProblem> problems)
    {
        if (document.Rows.Length != document.Height)
        {
            problems.Add(new("$.rows", $"Must have {document.Height} rows (the height), not {document.Rows.Length}."));
        }

        var fallback = legend.Values.First();
        var map = GameMap.Create(document.Width, document.Height, fallback);
        var tiles = map.Tiles.ToBuilder();
        for (var row = 0; row < Math.Min(document.Height, document.Rows.Length); row++)
        {
            // The parser's nullable checks don't reach collection elements.
            var text = document.Rows[row] ?? string.Empty;
            if (text.Length != document.Width)
            {
                problems.Add(new($"$.rows[{row}]", $"Must have {document.Width} characters (the width), not {text.Length}."));
            }

            for (var column = 0; column < Math.Min(document.Width, text.Length); column++)
            {
                if (legend.TryGetValue(text[column], out var terrain))
                {
                    tiles[(row * document.Width) + column] = tiles[(row * document.Width) + column] with { Terrain = terrain };
                }
                else
                {
                    problems.Add(new($"$.rows[{row}]", $"'{text[column]}' in column {column} is not in the legend."));
                }
            }
        }

        return map with { Tiles = tiles.MoveToImmutable() };
    }

    private static GameMap ReadRivers(Ruleset ruleset, PredefinedMapDocument document, GameMap map, List<RulesetProblem> problems)
    {
        var seen = new HashSet<HexEdge>();
        for (var index = 0; index < document.Rivers.Length; index++)
        {
            var river = document.Rivers[index];
            var path = $"$.rivers[{index}]";
            if (ReadEdge(river, map, path, problems) is not { } edge)
            {
                continue;
            }

            if (!ruleset.Rivers.Any(definition => definition.Id == river.River))
            {
                problems.Add(new($"{path}.river", $"'{river.River.Id}' is not a river of the ruleset."));
            }
            else if (!seen.Add(edge))
            {
                problems.Add(new(path, "The edge has a river already."));
            }
            else
            {
                map = map.WithRiver(edge, river.River);
            }
        }

        return map;
    }

    // The edge of one river entry, when its tile, side and the tile across it are all on the map.
    private static HexEdge? ReadEdge(PredefinedRiverDocument? river, GameMap map, string path, List<RulesetProblem> problems)
    {
        // The parser's nullable checks don't reach collection elements.
        if (river is null)
        {
            problems.Add(new(path, "Must be an object with tile, side and river."));
            return null;
        }

        if (ReadCoord(river.Tile, map, $"{path}.tile", problems) is not { } owner)
        {
            return null;
        }

        if (!Enum.IsDefined(river.Side))
        {
            problems.Add(new($"{path}.side", "Must be east, northEast or northWest."));
            return null;
        }

        var edge = new HexEdge(owner, river.Side);
        if (!map.Contains(edge.Neighbor()))
        {
            problems.Add(new(path, "The edge lies on the map's border: the tile across it is off the map."));
            return null;
        }

        return edge;
    }

    private static ImmutableArray<HexCoord> ReadSpawns(PredefinedMapDocument document, GameMap map, List<RulesetProblem> problems)
    {
        if (document.Spawns.Length < MinSpawns)
        {
            problems.Add(new("$.spawns", $"At least {MinSpawns} spawn tiles are required, so the opponent's start stays uncertain."));
        }

        var spawns = new List<HexCoord>();
        for (var index = 0; index < document.Spawns.Length; index++)
        {
            if (ReadCoord(document.Spawns[index], map, $"$.spawns[{index}]", problems) is not { } spawn)
            {
                continue;
            }

            if (spawns.Contains(spawn))
            {
                problems.Add(new($"$.spawns[{index}]", "The tile is listed twice."));
            }
            else
            {
                spawns.Add(spawn);
            }
        }

        return [.. spawns];
    }

    // A [column, row] pair on the map.
    private static HexCoord? ReadCoord(ImmutableArray<int> pair, GameMap map, string path, List<RulesetProblem> problems)
    {
        if (pair.IsDefault || pair.Length != 2)
        {
            problems.Add(new(path, "Must be [column, row]."));
            return null;
        }

        var coord = HexCoord.FromOffset(pair[0], pair[1]);
        if (!map.Contains(coord))
        {
            problems.Add(new(path, $"[{pair[0]}, {pair[1]}] is off the map."));
            return null;
        }

        return coord;
    }
}
