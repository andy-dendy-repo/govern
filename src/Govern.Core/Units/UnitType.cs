namespace Govern.Core.Units;

/// <summary>
/// Kind of unit; its stats and abilities live in <see cref="UnitCatalog"/>.
/// </summary>
public enum UnitType
{
    /// <summary>Civilian that founds a city and is consumed doing so.</summary>
    Settler,

    /// <summary>Military unit that attacks enemy units and captures enemy cities.</summary>
    Soldier,
}
