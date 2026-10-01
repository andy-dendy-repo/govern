using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// The bridges on the three edges a tile owns (<see cref="HexSide"/>); a bridge only stands on an edge with a river. JSON leaves
/// out the sides without one.
/// </summary>
/// <param name="East">Whether the east edge has a bridge.</param>
/// <param name="NorthEast">Whether the north-east edge has a bridge.</param>
/// <param name="NorthWest">Whether the north-west edge has a bridge.</param>
public sealed record TileBridges(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool East = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NorthEast = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NorthWest = false)
{
    /// <summary>Whether one side has a bridge.</summary>
    /// <param name="side">The side.</param>
    /// <returns><see langword="true"/> when that edge is bridged.</returns>
    public bool On(HexSide side) => side switch
    {
        HexSide.East => East,
        HexSide.NorthEast => NorthEast,
        HexSide.NorthWest => NorthWest,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };

    /// <summary>A copy with a bridge on one side.</summary>
    /// <param name="side">The side.</param>
    /// <returns>The new value.</returns>
    public TileBridges With(HexSide side) => side switch
    {
        HexSide.East => this with { East = true },
        HexSide.NorthEast => this with { NorthEast = true },
        HexSide.NorthWest => this with { NorthWest = true },
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };
}
