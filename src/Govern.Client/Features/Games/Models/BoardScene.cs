namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the 3D board draws (<c>wwwroot/js/board3d</c>), built from a player's view by <see cref="Helpers.BoardSceneBuilder"/>.
/// What shows is decided here in C#; the renderer only draws it and reports clicked tiles. Distances are in board units: a
/// hex's corner is 1 unit from its centre.
/// </summary>
/// <param name="Tiles">Every tile of the map, row by row.</param>
/// <param name="Map">The rectangle (x and depth) holding the whole map, for Fit.</param>
/// <param name="Start">The rectangle the board opens on: the player's units and cities with some room, else the whole map.</param>
public sealed record BoardScene(IReadOnlyList<SceneTile> Tiles, BoardRect Map, BoardRect Start);
