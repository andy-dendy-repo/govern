using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A unit as a player sees it. Movement left is shown only for the viewer's own units: an enemy's would reveal how far the
/// unit moved through fog this turn, or that it attacked.
/// </summary>
/// <param name="Id">Unit identifier.</param>
/// <param name="Owner">Player who controls the unit.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Position">Tile the unit stands on.</param>
/// <param name="MovementLeft">Movement points left this turn; <see langword="null"/> for an enemy unit.</param>
public sealed record UnitView(UnitId Id, PlayerId Owner, UnitType Type, HexCoord Position, int? MovementLeft)
{
    /// <summary>Rules for this unit's type (not serialized).</summary>
    [JsonIgnore]
    public UnitDefinition Definition => UnitCatalog.Get(Type);

    /// <summary>Projects a unit for a viewer, hiding the movement of units the viewer doesn't own.</summary>
    /// <param name="unit">The unit.</param>
    /// <param name="viewer">The player the view is for.</param>
    /// <returns>The unit as the viewer sees it.</returns>
    public static UnitView From(Unit unit, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return new UnitView(unit.Id, unit.Owner, unit.Type, unit.Position, unit.Owner == viewer ? unit.MovementLeft : null);
    }
}
