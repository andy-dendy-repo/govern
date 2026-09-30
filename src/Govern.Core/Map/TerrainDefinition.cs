namespace Govern.Core.Map;

/// <summary>
/// Rules data for one <see cref="TerrainType"/>, from the game's ruleset.
/// </summary>
/// <param name="Id">The terrain this definition describes.</param>
/// <param name="MovementCost">Movement points a unit spends to enter the tile (at least 1).</param>
/// <param name="IsPassable">Whether land units may enter the tile at all.</param>
/// <param name="SightBonus">Extra sight, in tiles, for a unit or city standing on the tile (0 for none).</param>
public sealed record TerrainDefinition(TerrainType Id, int MovementCost, bool IsPassable, int SightBonus);
