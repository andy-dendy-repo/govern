namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the 3D board animates right now, in board units: the <see cref="BoardMotion"/> of <c>BoardAnimationService</c> converted by
/// <see cref="Helpers.SceneMotionBuilder"/>. The renderer tweens each moving piece towards its new place and draws the ghosts and
/// effects; what plays and when is decided by the service, never by the renderer.
/// </summary>
/// <param name="Positions">Units drawn somewhere else than their tile.</param>
/// <param name="Ghosts">Units drawn although the view no longer holds them.</param>
/// <param name="Effects">Damage numbers and pulses.</param>
/// <param name="Step">How long a unit takes per tile, in milliseconds.</param>
/// <param name="Lunge">How long an attacker's lunge takes there and back, in milliseconds.</param>
/// <param name="Lifetime">How long effects and fading ghosts stay, in milliseconds.</param>
public sealed record SceneMotion(
    IReadOnlyList<SceneMotionPosition> Positions,
    IReadOnlyList<SceneGhost> Ghosts,
    IReadOnlyList<SceneEffect> Effects,
    double Step,
    double Lunge,
    double Lifetime);

/// <summary>A unit of the scene drawn at another place while it animates.</summary>
/// <param name="Unit">The unit's id (<see cref="ScenePiece.Unit"/>).</param>
/// <param name="X">Where it is drawn: x in board units.</param>
/// <param name="Z">Its depth.</param>
/// <param name="Height">The surface of the tile under that point.</param>
public sealed record SceneMotionPosition(int Unit, double X, double Z, double Height);

/// <summary>A unit drawn while it animates although the view no longer holds it.</summary>
/// <param name="Key">Unique while it is shown.</param>
/// <param name="Fading">Whether it fades out (destroyed) rather than walking.</param>
/// <param name="Piece">The unit's model, seat and place.</param>
public sealed record SceneGhost(int Key, bool Fading, ScenePiece Piece);

/// <summary>A short effect on the 3D board.</summary>
/// <param name="Key">Unique while it is shown.</param>
/// <param name="Kind"><c>damage</c> or <c>pulse</c> (<see cref="Enums.BoardEffectKind"/>, in lower case).</param>
/// <param name="X">Where: x in board units.</param>
/// <param name="Z">Its depth.</param>
/// <param name="Height">The surface of the tile there.</param>
/// <param name="Text">The number a damage effect shows.</param>
public sealed record SceneEffect(int Key, string Kind, double X, double Z, double Height, string? Text);
