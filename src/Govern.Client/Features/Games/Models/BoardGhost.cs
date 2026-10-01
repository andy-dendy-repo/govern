using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Models;

/// <summary>A unit drawn while it animates although the view no longer holds it.</summary>
/// <param name="Key">Unique while it is shown (a render key).</param>
/// <param name="Owner">Its owner (seat colour).</param>
/// <param name="Type">Its type (icon).</param>
/// <param name="At">Where it is drawn.</param>
/// <param name="Fading">Whether it fades out (destroyed) rather than walking.</param>
public sealed record BoardGhost(int Key, PlayerId Owner, UnitType Type, BoardPoint At, bool Fading);
