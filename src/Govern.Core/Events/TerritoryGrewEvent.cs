using Govern.Core.Cities;
using Govern.Core.Map;

namespace Govern.Core.Events;

/// <summary>
/// A city spent its growth resource and claimed one more tile.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="Tile">The tile it claimed.</param>
public sealed record TerritoryGrewEvent(CityId CityId, HexCoord Tile) : GameEvent;
