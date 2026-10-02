namespace Govern.Client.Features.Games.Models;

/// <summary>A bridge over a river edge of a <see cref="BoardScene"/>: a plank deck from one tile to the other, sloping between their heights.</summary>
/// <param name="X1">The end on the first tile: x in board units.</param>
/// <param name="Z1">The end on the first tile: depth.</param>
/// <param name="Y1">The end on the first tile: height (that tile's surface).</param>
/// <param name="X2">The end on the second tile: x.</param>
/// <param name="Z2">The end on the second tile: depth.</param>
/// <param name="Y2">The end on the second tile: height.</param>
public sealed record SceneBridge(double X1, double Z1, double Y1, double X2, double Z2, double Y2);
