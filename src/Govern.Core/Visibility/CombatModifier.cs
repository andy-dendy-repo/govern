namespace Govern.Core.Visibility;

/// <summary>
/// One change to a unit's strength in a fight.
/// </summary>
/// <param name="Kind">Why the strength changes.</param>
/// <param name="Value">Strength points added (negative for a loss).</param>
/// <param name="Subject">
/// The opponent's class for <see cref="CombatModifierKind.Counter"/>, the terrain id for <see cref="CombatModifierKind.Terrain"/>,
/// <see langword="null"/> for <see cref="CombatModifierKind.Health"/>.
/// </param>
public sealed record CombatModifier(CombatModifierKind Kind, int Value, string? Subject);
