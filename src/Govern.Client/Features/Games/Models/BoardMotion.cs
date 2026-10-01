using System.Collections.Immutable;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the board draws differently while animations play (<c>BoardAnimationService</c>): units shown somewhere else than
/// the view says, units no longer in the view (walking out of sight, fading after being destroyed) and effects. Replaced,
/// never changed, so the board can compare it by reference.
/// </summary>
/// <param name="Positions">Units drawn at a board point instead of their tile.</param>
/// <param name="Ghosts">Units drawn although the view no longer has them.</param>
/// <param name="Effects">Damage numbers and pulses.</param>
public sealed record BoardMotion(
    ImmutableDictionary<UnitId, BoardPoint> Positions,
    ImmutableList<BoardGhost> Ghosts,
    ImmutableList<BoardEffect> Effects)
{
    /// <summary>Nothing animates: the board draws the view as it is.</summary>
    public static BoardMotion None { get; } = new(ImmutableDictionary<UnitId, BoardPoint>.Empty, [], []);
}
