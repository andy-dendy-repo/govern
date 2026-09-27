namespace Govern.Core.Map;

/// <summary>
/// Rules data for one <see cref="TerrainType"/>.
/// </summary>
/// <param name="Type">The terrain this definition describes.</param>
/// <param name="MovementCost">Movement points a unit spends to enter the tile.</param>
/// <param name="IsPassable">Whether land units may enter the tile at all.</param>
public sealed record TerrainDefinition(TerrainType Type, int MovementCost, bool IsPassable);
