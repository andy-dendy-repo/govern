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

    /// <summary>Attacks adjacent enemy units and captures undefended enemy cities.</summary>
    [JsonStringEnumMemberName("attack")]
    Attack,
}
