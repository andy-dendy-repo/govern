using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.State;

namespace Govern.Core.Units;

/// <summary>
/// A unit on the map. At most one unit stands on a tile.
/// </summary>
/// <param name="Id">Unit identifier.</param>
/// <param name="Owner">Player who controls the unit.</param>
/// <param name="Type">Unit type; stats come from <see cref="UnitCatalog"/>.</param>
/// <param name="Position">Tile the unit stands on.</param>
/// <param name="MovementLeft">Movement points left this turn.</param>
public sealed record Unit(UnitId Id, PlayerId Owner, UnitType Type, HexCoord Position, int MovementLeft)
{
    /// <summary>The rules for this unit's type.</summary>
    [JsonIgnore]
    public UnitDefinition Definition => UnitCatalog.Get(Type);
}
