namespace Govern.Core.Units;

/// <summary>
/// Rules data for one <see cref="UnitType"/>.
/// </summary>
/// <param name="Type">The unit type this definition describes.</param>
/// <param name="Movement">Movement points restored at the start of the owner's turn.</param>
/// <param name="Sight">Radius, in tiles, the unit reveals around itself.</param>
/// <param name="ProductionCost">Production points a city needs to build the unit.</param>
/// <param name="CanFoundCity">Whether the unit can found a city (and is consumed doing so).</param>
/// <param name="CanAttack">Whether the unit can attack adjacent enemy units and cities.</param>
public sealed record UnitDefinition(UnitType Type, int Movement, int Sight, int ProductionCost, bool CanFoundCity, bool CanAttack);
