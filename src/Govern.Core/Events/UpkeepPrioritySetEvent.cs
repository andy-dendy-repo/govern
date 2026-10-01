using Govern.Core.Cities;

namespace Govern.Core.Events;

/// <summary>A city's owner chose whether it pays its buildings' or its units' upkeep first.</summary>
/// <param name="CityId">The city.</param>
/// <param name="Priority">The new order.</param>
public sealed record UpkeepPrioritySetEvent(CityId CityId, UpkeepPriority Priority) : GameEvent;
