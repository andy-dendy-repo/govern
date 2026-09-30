namespace Govern.Core.Visibility;

/// <summary>
/// Why a unit's strength changes in a fight (<see cref="CombatModifier"/>).
/// </summary>
public enum CombatModifierKind
{
    /// <summary>A hurt unit loses up to half its strength.</summary>
    Health,

    /// <summary>Its class is strong against the opponent's (<see cref="Rulesets.CombatRules.Counters"/>).</summary>
    Counter,

    /// <summary>The defender's tile gives defence (<see cref="Map.TerrainDefinition.DefenceBonus"/>).</summary>
    Terrain,
}
