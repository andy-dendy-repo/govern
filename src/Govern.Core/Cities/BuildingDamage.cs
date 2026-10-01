using Govern.Core.Economy;

namespace Govern.Core.Cities;

/// <summary>The health a city's building has lost to unpaid upkeep.</summary>
/// <param name="Building">The building.</param>
/// <param name="Damage">Health lost (1 up to <see cref="Rulesets.UpkeepRules.BuildingHealth"/>, where it gives nothing).</param>
public sealed record BuildingDamage(BuildingType Building, int Damage);
