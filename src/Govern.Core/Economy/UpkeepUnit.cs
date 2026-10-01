using Govern.Core.Units;

namespace Govern.Core.Economy;

/// <summary>A unit a city keeps up, with what decides its place in line.</summary>
/// <param name="Id">The unit.</param>
/// <param name="Type">Its type (its upkeep).</param>
/// <param name="Damage">Health it has lost; the most hurt are paid first.</param>
public sealed record UpkeepUnit(UnitId Id, UnitType Type, int Damage);
