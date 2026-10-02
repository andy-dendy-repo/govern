namespace Govern.Client.Features.Games.Models;

/// <summary>A lens badge on a tile edge (such as a river crossing's extra cost), drawn as a small disc with its label.</summary>
/// <param name="X">The edge middle's x in board units.</param>
/// <param name="Z">The edge middle's depth.</param>
/// <param name="Height">The higher of the two tiles' surfaces, so the badge stands clear of both.</param>
/// <param name="Label">The short text.</param>
/// <param name="Mark">The lens's class for the value (<c>blocked</c>, <c>costly</c>), which styles the badge.</param>
public sealed record OverlayEdge(double X, double Z, double Height, string Label, string Mark);
