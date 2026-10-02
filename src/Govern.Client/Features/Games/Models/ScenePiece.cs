namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A unit or a city on a <see cref="BoardScene"/>: a small model in its owner's seat colour, with its icon on a banner above it
/// so it reads at any zoom (and, for a city, its name; for a hurt unit, its health).
/// </summary>
/// <param name="Q">The tile's axial column (clicking the piece clicks its tile).</param>
/// <param name="R">The tile's axial row.</param>
/// <param name="X">The model's x in board units.</param>
/// <param name="Z">The model's depth.</param>
/// <param name="Height">The tile's surface, where the model stands.</param>
/// <param name="Kind"><c>unit</c> or <c>city</c>.</param>
/// <param name="Model">The model, in lower case (<see cref="Enums.PieceModel"/>).</param>
/// <param name="Seat">The owner's seat number (<c>--gv-seat-&lt;n&gt;</c>).</param>
/// <param name="Own">Whether the piece is the player's own.</param>
/// <param name="Health">A unit's health as a share of full health (1 unhurt; a bar shows below 1); 1 for a city.</param>
/// <param name="Name">A city's name, drawn under it; <see langword="null"/> for a unit.</param>
/// <param name="Icon">The id of the piece's symbol in the game's sprite (<c>Helpers/Icons</c>), shown on its banner.</param>
public sealed record ScenePiece(
    int Q, int R, double X, double Z, double Height, string Kind, string Model, int Seat, bool Own, double Health, string? Name, string Icon);
