namespace Govern.Client.Features.Games.Models;

/// <summary>A tile tinted by a <see cref="BoardOverlay"/>: a highlight for the selection, or a lens mark with its label.</summary>
/// <param name="X">The tile centre's x in board units.</param>
/// <param name="Z">The tile centre's depth.</param>
/// <param name="Height">The tile's surface, where the tint lies.</param>
/// <param name="Tint">The colour token of the tint (an <c>rgba()</c> token, such as <c>--gv-overlay-reachable</c> or <c>--gv-lens-costly</c>).</param>
/// <param name="Label">A lens mark's short text, or <see langword="null"/> for a highlight.</param>
public sealed record OverlayTile(double X, double Z, double Height, string Tint, string? Label = null);
