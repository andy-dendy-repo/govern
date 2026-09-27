using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A settler founded a city and was consumed.
/// </summary>
/// <param name="City">The new city.</param>
/// <param name="SettlerId">The settler that founded it.</param>
public sealed record CityFoundedEvent(City City, UnitId SettlerId) : GameEvent;
