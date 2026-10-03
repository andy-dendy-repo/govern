using System.Collections.Immutable;

namespace Govern.Core.Map;

// A map document as written, before validation: one string per row with one legend character per tile, the rivers by edge, and
// the first player's spawn candidates as [column, row].
internal sealed record PredefinedMapDocument(
    string Key,
    string Name,
    int Width,
    int Height,
    ImmutableDictionary<string, TerrainType> Legend,
    ImmutableArray<string> Rows,
    ImmutableArray<PredefinedRiverDocument> Rivers,
    ImmutableArray<ImmutableArray<int>> Spawns);
