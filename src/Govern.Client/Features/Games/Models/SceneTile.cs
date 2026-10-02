namespace Govern.Client.Features.Games.Models;

/// <summary>One tile of a <see cref="BoardScene"/>.</summary>
/// <param name="Q">The tile's axial column, reported back when it is clicked.</param>
/// <param name="R">The tile's axial row.</param>
/// <param name="X">The centre's x in board units (a hex's corner is 1 unit from its centre).</param>
/// <param name="Z">The centre's depth in board units; north is away from the camera.</param>
/// <param name="Terrain">The terrain id (its colour is the <c>--gv-terrain-&lt;id&gt;</c> token), or <see langword="null"/> when unexplored.</param>
/// <param name="Visibility"><c>visible</c>, <c>explored</c> or <c>unexplored</c>.</param>
/// <param name="Height">The tile top's height (<see cref="TileRelief.Height"/>).</param>
/// <param name="Shape">What stands on the tile, in lower case (<see cref="Enums.TileShape"/>).</param>
public sealed record SceneTile(int Q, int R, double X, double Z, string? Terrain, string Visibility, double Height, string Shape);
