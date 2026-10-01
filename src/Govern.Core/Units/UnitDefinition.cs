using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Rulesets;

namespace Govern.Core.Units;

/// <summary>
/// Rules data for one <see cref="UnitType"/>, from the game's ruleset.
/// </summary>
/// <param name="Id">The unit type this definition describes.</param>
/// <param name="Class">
/// Its combat class (a free id such as <c>melee</c> or <c>cavalry</c>), which <see cref="CombatRules.Counters"/> refer to.
/// </param>
/// <param name="Movement">Movement points restored at the start of the owner's turn.</param>
/// <param name="Sight">Radius, in tiles, the unit reveals around itself.</param>
/// <param name="ProductionCost">Production points a city needs to build the unit.</param>
/// <param name="Abilities">What the unit can do.</param>
/// <param name="Strength">
/// Combat strength when attacking in melee or defending; <see langword="null"/> for a civilian, which any attack destroys and
/// which never fights back.
/// </param>
/// <param name="Ranged">The ranged attack; set exactly when the unit has <see cref="UnitAbility.RangedAttack"/>.</param>
/// <param name="BuildProduction">
/// Production points the unit puts into its job each turn; at least 1 exactly when it has <see cref="UnitAbility.BuildImprovement"/>
/// or <see cref="UnitAbility.BuildBridge"/>, otherwise 0.
/// </param>
public sealed record UnitDefinition(
    UnitType Id,
    string Class,
    int Movement,
    int Sight,
    int ProductionCost,
    ImmutableArray<UnitAbility> Abilities,
    int? Strength = null,
    RangedRules? Ranged = null,
    int BuildProduction = 0)
{
    /// <summary>Whether the unit can found a city (and is consumed doing so).</summary>
    [JsonIgnore]
    public bool CanFoundCity => Abilities.Contains(UnitAbility.FoundCity);

    /// <summary>Whether the unit can attack adjacent enemy units and cities in melee.</summary>
    [JsonIgnore]
    public bool CanAttack => Abilities.Contains(UnitAbility.Attack);

    /// <summary>Whether the unit can shoot enemy units within <see cref="RangedRules.Range"/>.</summary>
    [JsonIgnore]
    public bool CanRangedAttack => Abilities.Contains(UnitAbility.RangedAttack);

    /// <summary>Whether the unit can build tile improvements.</summary>
    [JsonIgnore]
    public bool CanBuildImprovements => Abilities.Contains(UnitAbility.BuildImprovement);

    /// <summary>Whether the unit can build bridges.</summary>
    [JsonIgnore]
    public bool CanBuildBridges => Abilities.Contains(UnitAbility.BuildBridge);

    /// <summary>The farthest tile the unit can attack: its ranged range, 1 for melee, 0 when it can't attack.</summary>
    [JsonIgnore]
    public int AttackRange => CanRangedAttack ? Ranged?.Range ?? 0 : CanAttack ? 1 : 0;
}
