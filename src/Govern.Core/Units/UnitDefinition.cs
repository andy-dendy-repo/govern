using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Govern.Core.Units;

/// <summary>
/// Rules data for one <see cref="UnitType"/>, from the game's ruleset.
/// </summary>
/// <param name="Id">The unit type this definition describes.</param>
/// <param name="Movement">Movement points restored at the start of the owner's turn.</param>
/// <param name="Sight">Radius, in tiles, the unit reveals around itself.</param>
/// <param name="ProductionCost">Production points a city needs to build the unit.</param>
/// <param name="Abilities">What the unit can do.</param>
public sealed record UnitDefinition(UnitType Id, int Movement, int Sight, int ProductionCost, ImmutableArray<UnitAbility> Abilities)
{
    /// <summary>Whether the unit can found a city (and is consumed doing so).</summary>
    [JsonIgnore]
    public bool CanFoundCity => Abilities.Contains(UnitAbility.FoundCity);

    /// <summary>Whether the unit can attack adjacent enemy units and cities.</summary>
    [JsonIgnore]
    public bool CanAttack => Abilities.Contains(UnitAbility.Attack);
}
