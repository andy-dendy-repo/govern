using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Units;

/// <summary>
/// A unit on the map. At most one unit stands on a tile.
/// </summary>
/// <param name="Id">Unit identifier.</param>
/// <param name="Owner">Player who controls the unit.</param>
/// <param name="Type">Unit type; its stats come from the game's ruleset (<see cref="Rulesets.Ruleset.Unit"/>).</param>
/// <param name="Position">Tile the unit stands on.</param>
/// <param name="MovementLeft">Movement points left this turn.</param>
/// <param name="Damage">
/// Health lost (0 = unhurt); its health is <see cref="Rulesets.CombatRules.MaxHealth"/> minus this. Stored as damage, not health,
/// so games saved before health existed read as unhurt, and the JSON leaves it out while it is 0.
/// </param>
/// <param name="Job">What the unit is building, if anything; left out of the JSON while idle.</param>
public sealed record Unit(
    UnitId Id,
    PlayerId Owner,
    UnitType Type,
    HexCoord Position,
    int MovementLeft,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Damage = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitJob? Job = null)
{
    /// <summary>The unit's health under a ruleset: its maximum minus <see cref="Damage"/>.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <returns>Health, from 1 to <see cref="Rulesets.CombatRules.MaxHealth"/> for a unit on the map.</returns>
    public int Health(Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return ruleset.Combat.MaxHealth - Damage;
    }
}
