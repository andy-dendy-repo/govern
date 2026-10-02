namespace Govern.Client.Features.Games.Models;

/// <summary>How a river kind looks on the 3D board.</summary>
/// <param name="Width">The water's width in board units (a hex's corner is 1 unit from its centre).</param>
/// <param name="Deep">Whether it takes the deep river colour (<c>--gv-river-deep</c>) instead of <c>--gv-river</c>.</param>
public sealed record RiverRelief(double Width, bool Deep);
