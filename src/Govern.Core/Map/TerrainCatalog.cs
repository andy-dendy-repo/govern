namespace Govern.Core.Map;

/// <summary>
/// Rules data for every terrain type. Adding terrain means adding a <see cref="TerrainType"/> value and an entry here.
/// </summary>
public static class TerrainCatalog
{
    private static readonly TerrainDefinition Plains = new(TerrainType.Plains, MovementCost: 1, IsPassable: true);

    /// <summary>Returns the rules for a terrain type.</summary>
    /// <param name="type">The terrain type.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a known terrain.</exception>
    public static TerrainDefinition Get(TerrainType type) => type switch
    {
        TerrainType.Plains => Plains,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown terrain type."),
    };
}
