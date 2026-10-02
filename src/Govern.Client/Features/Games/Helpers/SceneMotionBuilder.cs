using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Converts the <see cref="BoardMotion"/> of <see cref="BoardAnimationService"/> (in 2D board points) into the
/// <see cref="SceneMotion"/> the 3D board draws (in board units, with the surface of the tile under each point), so both boards
/// play the same animations from one source.
/// </summary>
public static class SceneMotionBuilder
{
    /// <summary>Builds the scene motion of a board motion.</summary>
    /// <param name="motion">What the animation service draws differently.</param>
    /// <param name="view">The view the board draws (for seats and tile surfaces); <see langword="null"/> before one is drawn.</param>
    /// <returns>The scene motion.</returns>
    public static SceneMotion Build(BoardMotion motion, PlayerView? view)
    {
        ArgumentNullException.ThrowIfNull(motion);
        var positions = view is null
            ? []
            : motion.Positions.Select(pair =>
            {
                var (x, z, height) = Place(view, pair.Value);
                return new SceneMotionPosition(pair.Key.Value, x, z, height);
            }).OrderBy(static position => position.Unit).ToList();
        var ghosts = view is null
            ? []
            : motion.Ghosts.Select(ghost =>
            {
                var (x, z, height) = Place(view, ghost.At);
                var piece = ScenePieceBuilder.Ghost(view, HexLayout.Nearest(ghost.At.X, ghost.At.Y), ghost.Type, ghost.Owner);
                return new SceneGhost(ghost.Key, ghost.Fading, piece with { X = x, Z = z, Height = height });
            }).ToList();
        var effects = view is null
            ? []
            : motion.Effects.Select(effect =>
            {
                var (x, z, height) = Place(view, effect.At);
                return new SceneEffect(effect.Key, effect.Kind.ToString().ToLowerInvariant(), x, z, height, effect.Text);
            }).ToList();
        return new SceneMotion(
            positions,
            ghosts,
            effects,
            BoardAnimationService.Step.TotalMilliseconds,
            BoardAnimationService.Lunge.TotalMilliseconds,
            BoardAnimationService.EffectLifetime.TotalMilliseconds);
    }

    // A 2D board point in board units, with the surface of the tile under it (the lowest ground off the map).
    private static (double X, double Z, double Height) Place(PlayerView view, BoardPoint point)
    {
        var tile = HexLayout.Nearest(point.X, point.Y);
        var height = view.Contains(tile) ? TerrainReliefs.Surface(view.TileAt(tile)) : TerrainReliefs.Unexplored.Height;
        return (point.X / HexLayout.Size, point.Y / HexLayout.Size, height);
    }
}
