using Govern.Core.Units;

namespace Govern.Core.Commands;

/// <summary>
/// Founds a city on the settler's tile; the settler is consumed.
/// </summary>
/// <param name="UnitId">The actor's settler.</param>
public sealed record FoundCityCommand(UnitId UnitId) : GameCommand;
