using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A builder finished upgrading a building on its tile. Seen by its owner, and without the city by players who see the tile.
/// </summary>
/// <param name="UnitId">The builder.</param>
/// <param name="Owner">The builder's (and the city's) owner.</param>
/// <param name="Position">The building's tile.</param>
/// <param name="From">The building before.</param>
/// <param name="To">The building now.</param>
/// <param name="CityId">The city the building belongs to; <see langword="null"/> for a player who doesn't own it.</param>
public sealed record BuildingUpgradedEvent(UnitId UnitId, PlayerId Owner, HexCoord Position, BuildingType From, BuildingType To, CityId? CityId) : GameEvent;
