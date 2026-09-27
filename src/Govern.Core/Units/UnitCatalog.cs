namespace Govern.Core.Units;

/// <summary>
/// Rules data for every unit type. Adding a unit means adding a <see cref="UnitType"/> value and an entry here;
/// a new ability also needs a command and a rule.
/// </summary>
public static class UnitCatalog
{
    private static readonly UnitDefinition Settler =
        new(UnitType.Settler, Movement: 2, Sight: 2, ProductionCost: 6, CanFoundCity: true, CanAttack: false);

    private static readonly UnitDefinition Soldier =
        new(UnitType.Soldier, Movement: 2, Sight: 2, ProductionCost: 4, CanFoundCity: false, CanAttack: true);

    /// <summary>Returns the rules for a unit type.</summary>
    /// <param name="type">The unit type.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a known unit type.</exception>
    public static UnitDefinition Get(UnitType type) => type switch
    {
        UnitType.Settler => Settler,
        UnitType.Soldier => Soldier,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type."),
    };
}
