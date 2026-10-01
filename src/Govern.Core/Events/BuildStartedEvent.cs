using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit started building an improvement or a bridge.
/// </summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Job">What it builds.</param>
public sealed record BuildStartedEvent(UnitId UnitId, UnitJob Job) : GameEvent;
