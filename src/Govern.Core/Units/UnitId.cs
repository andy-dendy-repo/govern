namespace Govern.Core.Units;

/// <summary>
/// Identifier of a unit, unique within one game (allocated from <c>GameState.NextId</c>).
/// </summary>
/// <param name="Value">Positive number.</param>
public readonly record struct UnitId(int Value);
