using Govern.Core.Hexes;
using Govern.Core.Players;
using Govern.Core.Units;

namespace Govern.Core.Cities;

/// <summary>
/// A city. It adds <see cref="ProductionPerTurn"/> to <see cref="ProductionProgress"/> at the end of its owner's turn and
/// spawns a <see cref="Producing"/> unit when the progress reaches the unit's cost.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">Player who controls the city.</param>
/// <param name="Name">Display name.</param>
/// <param name="Position">Tile the city stands on.</param>
/// <param name="Producing">Unit type the city is building.</param>
/// <param name="ProductionProgress">Production points accumulated towards <paramref name="Producing"/>.</param>
public sealed record City(CityId Id, PlayerId Owner, string Name, HexCoord Position, UnitType Producing, int ProductionProgress)
{
    /// <summary>Radius, in tiles, a city reveals around itself.</summary>
    public const int Sight = 2;

    /// <summary>Minimum hex distance between two cities.</summary>
    public const int MinimumSpacing = 3;

    /// <summary>Production points a city adds each turn.</summary>
    public const int ProductionPerTurn = 1;
}
