using System.Collections.Immutable;

namespace Govern.Core.Map;

// One river edge of a map document: the tile that owns the edge as [column, row], its side and the river on it.
internal sealed record PredefinedRiverDocument(ImmutableArray<int> Tile, HexSide Side, RiverType River);
