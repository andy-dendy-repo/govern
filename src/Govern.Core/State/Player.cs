using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.State;

/// <summary>
/// A participant and what they have discovered of the map.
/// </summary>
/// <param name="Id">Player identifier.</param>
/// <param name="Status">Whether the player still takes part.</param>
/// <param name="Explored">Every tile the player has ever seen, in discovery order; drives the fog of war.</param>
public sealed record Player(PlayerId Id, PlayerStatus Status, ImmutableArray<HexCoord> Explored);
