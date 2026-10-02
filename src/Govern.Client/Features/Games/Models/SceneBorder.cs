namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A stretch of territory border on a <see cref="BoardScene"/>: along a tile edge where a territory ends, set slightly inside the
/// owned tile so two neighbouring territories show both colours. Drawn as a dashed line in the owner's seat colour.
/// </summary>
/// <param name="X1">The first end's x in board units.</param>
/// <param name="Z1">The first end's depth.</param>
/// <param name="X2">The second end's x.</param>
/// <param name="Z2">The second end's depth.</param>
/// <param name="Height">The owned tile's surface (its top, or the water over a lake).</param>
/// <param name="Seat">The owner's seat number (<c>--gv-seat-&lt;n&gt;</c>).</param>
public sealed record SceneBorder(double X1, double Z1, double X2, double Z2, double Height, int Seat);
