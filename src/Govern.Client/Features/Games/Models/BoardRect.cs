namespace Govern.Client.Features.Games.Models;

/// <summary>A rectangle in pixels: the map's extent in board space, or the board element on screen.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Width">The width; not negative.</param>
/// <param name="Height">The height; not negative.</param>
public sealed record BoardRect(double Left, double Top, double Width, double Height);
