using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>The tiles marked on the board for the selected unit.</summary>
/// <param name="Reachable">Tiles the unit can move to now.</param>
/// <param name="Attackable">Tiles the unit can attack now.</param>
public sealed record BoardHighlights(ImmutableHashSet<HexCoord> Reachable, ImmutableHashSet<HexCoord> Attackable)
{
    /// <summary>No tiles marked.</summary>
    public static BoardHighlights None { get; } = new([], []);
}
