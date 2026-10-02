using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>How a terrain looks on the 3D board: the height of its tile top and what stands on it.</summary>
/// <param name="Height">The tile top's height in board units (a hex's corner is 1 unit from its centre).</param>
/// <param name="Shape">What the renderer builds on the tile.</param>
public sealed record TileRelief(double Height, TileShape Shape);
