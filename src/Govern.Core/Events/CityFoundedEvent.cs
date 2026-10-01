using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A settler founded a city and was consumed. Only public facts, so the event can reach the other player as it is.
/// </summary>
/// <param name="CityId">The new city.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Position">Its tile.</param>
/// <param name="Name">Its name.</param>
/// <param name="SettlerId">The settler that founded it.</param>
public sealed record CityFoundedEvent(CityId CityId, PlayerId Owner, HexCoord Position, string Name, UnitId SettlerId) : GameEvent;
