using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// The rivers on the three edges a tile owns (<see cref="HexSide"/>). JSON leaves out the sides without a river.
/// </summary>
/// <param name="East">The river on the east edge, if any.</param>
/// <param name="NorthEast">The river on the north-east edge, if any.</param>
/// <param name="NorthWest">The river on the north-west edge, if any.</param>
public sealed record TileRivers(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RiverType? East = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RiverType? NorthEast = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RiverType? NorthWest = null)
{
    /// <summary>The river on one side.</summary>
    /// <param name="side">The side.</param>
    /// <returns>The river, or <see langword="null"/> when that edge has none.</returns>
    public RiverType? On(HexSide side) => side switch
    {
        HexSide.East => East,
        HexSide.NorthEast => NorthEast,
        HexSide.NorthWest => NorthWest,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };

    /// <summary>A copy with a river on one side.</summary>
    /// <param name="side">The side.</param>
    /// <param name="river">The river.</param>
    /// <returns>The new value.</returns>
    public TileRivers With(HexSide side, RiverType river) => side switch
    {
        HexSide.East => this with { East = river },
        HexSide.NorthEast => this with { NorthEast = river },
        HexSide.NorthWest => this with { NorthWest = river },
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };
}
