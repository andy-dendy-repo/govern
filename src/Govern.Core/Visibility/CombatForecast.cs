namespace Govern.Core.Visibility;

/// <summary>
/// What an attack on a unit can do, before it is made: both sides' strengths and the damage each can take. The engine rolls the
/// damage within these ranges, so the forecast always holds (<see cref="Engine.GameEngine.CombatForecast"/>).
/// </summary>
/// <param name="Attacker">The attacking unit's side; it takes no damage from a ranged attack or from a civilian.</param>
/// <param name="Defender">The attacked unit's side.</param>
/// <param name="IsRanged">Whether the attacker shoots from range, so the defender can't hit back.</param>
public sealed record CombatForecast(CombatSide Attacker, CombatSide Defender, bool IsRanged);
