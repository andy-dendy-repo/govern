using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A city spent its growth resource and claimed one more tile.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="Owner">The city's owner.</param>
/// <param name="Tile">The tile it claimed.</param>
public sealed record TerritoryGrewEvent(CityId CityId, PlayerId Owner, HexCoord Tile) : GameEvent;
