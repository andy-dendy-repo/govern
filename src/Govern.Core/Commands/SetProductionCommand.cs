using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Core.Commands;

/// <summary>
/// Chooses which unit a city builds; accumulated progress is kept.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="UnitType">Unit type to build.</param>
public sealed record SetProductionCommand(CityId CityId, UnitType UnitType) : GameCommand;
