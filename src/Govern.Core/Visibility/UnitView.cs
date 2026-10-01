using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A unit as a player sees it. Movement left is shown only for the viewer's own units: an enemy's would reveal how far the
/// unit moved through fog this turn, or that it attacked. Health is shown for every unit the viewer sees, as on the board; since a
/// hurt unit heals only when it kept all its movement and has no job, a hurt unit seen before and after its owner's turn reveals
/// whether it moved or is working (an accepted leak, see docs/game/game-logic.md).
/// </summary>
/// <param name="Id">Unit identifier.</param>
/// <param name="Owner">Player who controls the unit.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Position">Tile the unit stands on.</param>
/// <param name="MovementLeft">Movement points left this turn; <see langword="null"/> for an enemy unit.</param>
/// <param name="Health">Health left, from 1 to <see cref="CombatRules.MaxHealth"/>.</param>
/// <param name="Job">What the unit is building; only for the viewer's own units (an enemy's would show its plans).</param>
public sealed record UnitView(
    UnitId Id,
    PlayerId Owner,
    UnitType Type,
    HexCoord Position,
    int? MovementLeft,
    int Health,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitJob? Job = null)
{
    /// <summary>Projects a unit for a viewer, hiding the movement and job of units the viewer doesn't own.</summary>
    /// <param name="ruleset">The game's ruleset (for the unit's health).</param>
    /// <param name="unit">The unit.</param>
    /// <param name="viewer">The player the view is for.</param>
    /// <returns>The unit as the viewer sees it.</returns>
    public static UnitView From(Ruleset ruleset, Unit unit, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var own = unit.Owner == viewer;
        return new UnitView(unit.Id, unit.Owner, unit.Type, unit.Position, own ? unit.MovementLeft : null, unit.Health(ruleset), own ? unit.Job : null);
    }
}
