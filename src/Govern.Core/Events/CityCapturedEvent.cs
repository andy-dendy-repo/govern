using Govern.Core.Cities;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A soldier captured an undefended enemy city and moved into it.
/// </summary>
/// <param name="CityId">The city.</param>
/// <param name="PreviousOwner">Who lost it.</param>
/// <param name="NewOwner">Who took it.</param>
/// <param name="CapturedBy">The capturing unit.</param>
public sealed record CityCapturedEvent(CityId CityId, PlayerId PreviousOwner, PlayerId NewOwner, UnitId CapturedBy) : GameEvent;
