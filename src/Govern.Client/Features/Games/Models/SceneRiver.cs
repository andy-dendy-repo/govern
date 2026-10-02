namespace Govern.Client.Features.Games.Models;

/// <summary>A river along one tile edge of a <see cref="BoardScene"/>: a strip of water from corner to corner.</summary>
/// <param name="X1">The first corner's x in board units.</param>
/// <param name="Z1">The first corner's depth.</param>
/// <param name="X2">The second corner's x.</param>
/// <param name="Z2">The second corner's depth.</param>
/// <param name="Height">The water's height: the lower of the two tiles' surfaces (an unexplored neighbour doesn't count), so the higher tile's side makes the bank.</param>
/// <param name="Width">The water's width (<see cref="RiverRelief.Width"/>).</param>
/// <param name="Deep">Whether it takes the deep river colour (<see cref="RiverRelief.Deep"/>).</param>
/// <param name="Visibility"><c>visible</c> or <c>explored</c>: the visibility of the tile that holds the edge.</param>
public sealed record SceneRiver(double X1, double Z1, double X2, double Z2, double Height, double Width, bool Deep, string Visibility);
