using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A city finished producing a unit. Only public facts; the other player gets it without the city.
/// </summary>
/// <param name="UnitId">The new unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="UnitType">Its type.</param>
/// <param name="Position">Where it appeared (the city's tile).</param>
/// <param name="CityId">The city that produced it; <see langword="null"/> for a player who doesn't own it (it may stand out of sight).</param>
public sealed record UnitCreatedEvent(UnitId UnitId, PlayerId Owner, UnitType UnitType, HexCoord Position, CityId? CityId) : GameEvent;
