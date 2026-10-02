namespace Govern.Client.Features.Games.Models;

/// <summary>
/// An improvement or building on a tile of a <see cref="BoardScene"/>, drawn as a small model in the tile's upper-left corner
/// so the middle stays free for a unit or city.
/// </summary>
/// <param name="Q">The tile's axial column (the renderer keeps the tile's decorations clear of the model).</param>
/// <param name="R">The tile's axial row.</param>
/// <param name="X">The model's x in board units.</param>
/// <param name="Z">The model's depth.</param>
/// <param name="Height">The tile's surface (its top, or the water over a lake), where the model stands.</param>
/// <param name="Model">The model, in lower case (<see cref="Enums.FeatureModel"/>).</param>
/// <param name="Seat">The owner's seat number (<c>--gv-seat-&lt;n&gt;</c>) for a building; 0 for an improvement.</param>
/// <param name="Planned">Whether it is a building the player's city is still producing there (drawn faded).</param>
public sealed record SceneFeature(int Q, int R, double X, double Z, double Height, string Model, int Seat, bool Planned);
