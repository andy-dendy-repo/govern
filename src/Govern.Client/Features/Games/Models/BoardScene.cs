namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the 3D board draws (<c>wwwroot/js/board3d</c>), built from a player's view by <see cref="Helpers.BoardSceneBuilder"/>.
/// What shows is decided here in C#; the renderer only draws it and reports clicked tiles. Distances are in board units: a
/// hex's corner is 1 unit from its centre.
/// </summary>
/// <param name="Tiles">Every tile of the map, row by row.</param>
/// <param name="Map">The rectangle (x and depth) holding the whole map, for Fit.</param>
/// <param name="Start">The rectangle the board opens on: the player's units and cities with some room, else the whole map.</param>
/// <param name="Rivers">The known rivers, one per river edge.</param>
/// <param name="Bridges">The bridges in sight.</param>
/// <param name="Features">The improvements and buildings in sight, and the buildings the player's cities are producing.</param>
/// <param name="Borders">The territory borders in sight.</param>
public sealed record BoardScene(
    IReadOnlyList<SceneTile> Tiles,
    BoardRect Map,
    BoardRect Start,
    IReadOnlyList<SceneRiver> Rivers,
    IReadOnlyList<SceneBridge> Bridges,
    IReadOnlyList<SceneFeature> Features,
    IReadOnlyList<SceneBorder> Borders);
