namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the 3D board draws over its <see cref="BoardScene"/> for the player's current choice: the active lens, the selection's
/// highlights and the selected piece. Built by <see cref="Helpers.BoardOverlayBuilder"/> and sent apart from the scene, so
/// selecting, toggling a lens or choosing a target never rebuilds the map.
/// </summary>
/// <param name="Tiles">The tinted tiles: lens marks first, then highlights, which are drawn over them.</param>
/// <param name="Edges">The lens's edge badges.</param>
/// <param name="Selected">The tile of the selected unit or city, ringed; <see langword="null"/> when nothing is selected.</param>
public sealed record BoardOverlay(IReadOnlyList<OverlayTile> Tiles, IReadOnlyList<OverlayEdge> Edges, OverlayTile? Selected)
{
    /// <summary>Nothing over the board.</summary>
    public static BoardOverlay None { get; } = new([], [], null);
}
