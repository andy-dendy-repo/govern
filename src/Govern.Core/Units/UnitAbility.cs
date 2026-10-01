using System.Text.Json.Serialization;

namespace Govern.Core.Units;

/// <summary>
/// Something a unit type can do, listed in its ruleset entry (<see cref="UnitDefinition.Abilities"/>). Core defines what each
/// ability does; a ruleset only chooses which units have it.
/// </summary>
public enum UnitAbility
{
    /// <summary>Founds a city on its tile and is consumed doing so.</summary>
    [JsonStringEnumMemberName("found_city")]
    FoundCity,

    /// <summary>Attacks adjacent enemy units in melee and captures undefended enemy cities.</summary>
    [JsonStringEnumMemberName("attack")]
    Attack,

    /// <summary>
    /// Shoots an enemy unit within range (<see cref="UnitDefinition.Ranged"/>) without taking damage back; can't capture cities.
    /// </summary>
    [JsonStringEnumMemberName("ranged_attack")]
    RangedAttack,

    /// <summary>
    /// Builds tile improvements in its own cities' work areas, with its <see cref="UnitDefinition.BuildProduction"/> plus the
    /// city's production.
    /// </summary>
    [JsonStringEnumMemberName("build_improvement")]
    BuildImprovement,

    /// <summary>
    /// Builds bridges over rivers next to it, with its <see cref="UnitDefinition.BuildProduction"/> (plus the city's production
    /// inside one of its own cities' work areas).
    /// </summary>
    [JsonStringEnumMemberName("build_bridge")]
    BuildBridge,
}
