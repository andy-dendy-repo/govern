using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>The tiles marked on the board for the selected unit, or for the selected city placing a building.</summary>
/// <param name="Reachable">Tiles the unit can move to now.</param>
/// <param name="Attackable">Tiles the unit can attack now.</param>
/// <param name="Bridgeable">
/// In bridge mode, the neighbours the unit can bridge to (a bridge goes on the river edge between the unit's tile and the
/// target); empty otherwise.
/// </param>
/// <param name="Placeable">
/// While the selected city chooses a tile for a building (<see cref="BoardSelection.Placing"/>), the tiles that can take it;
/// empty otherwise.
/// </param>
public sealed record BoardHighlights(
    ImmutableHashSet<HexCoord> Reachable,
    ImmutableHashSet<HexCoord> Attackable,
    ImmutableHashSet<HexCoord> Bridgeable,
    ImmutableHashSet<HexCoord> Placeable)
{
    /// <summary>No tiles marked.</summary>
    public static BoardHighlights None { get; } = new([], [], [], []);
}
