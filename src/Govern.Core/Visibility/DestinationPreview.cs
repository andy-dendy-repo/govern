using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Visibility;

/// <summary>
/// The way one of the viewer's units would walk to a destination, from their view alone (for the client).
/// </summary>
/// <param name="Path">Tiles entered in order, ending with the destination.</param>
/// <param name="Turns">
/// How many of the viewer's coming turns the walk still needs if nothing gets in the way: 0 when the movement the unit has left
/// gets it there now, otherwise the number of turn starts at which it walks on with full movement.
/// </param>
public sealed record DestinationPreview(ImmutableArray<HexCoord> Path, int Turns);
