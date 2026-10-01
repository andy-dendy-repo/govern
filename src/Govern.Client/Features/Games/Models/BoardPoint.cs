namespace Govern.Client.Features.Games.Models;

/// <summary>A point in board space (the SVG coordinates of <c>HexLayout</c>).</summary>
/// <param name="X">Horizontal.</param>
/// <param name="Y">Vertical.</param>
public readonly record struct BoardPoint(double X, double Y);
