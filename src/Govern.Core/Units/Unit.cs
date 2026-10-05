using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Units;

/// <summary>
/// A unit on the map. At most one unit that holds its tile stands on a tile; traders (<see cref="UnitDefinition.SharesTiles"/>)
/// share tiles with any unit and never block one.
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
/// <param name="Destination">
/// The tile the unit walks to on its own, turn after turn (see <see cref="Actions.SetDestinationCommand"/>); left out of the JSON
/// while it has no such order.
/// </param>
/// <param name="DealId">
/// The agreed trade deal the unit, a trader, serves (G17b): it walks to the host's trade post and, once the deal is active,
/// back and forth between the two posts; its owner can't move it meanwhile. Left out of the JSON otherwise.
/// </param>
/// <param name="Exploring">
/// Whether the unit explores on its own (see <see cref="Actions.ExploreCommand"/>): turn after turn it walks to the nearest tile
/// its owner knows next to one they never saw. Left out of the JSON while it has no such order.
/// </param>
/// <param name="HomeCity">
/// The city a trader belongs to (G17b): the one that trained it, or the city it was moved to when that one was lost. Its deals
/// start there. Left out of the JSON for other units.
/// </param>
public sealed record Unit(
    UnitId Id,
    PlayerId Owner,
    UnitType Type,
    HexCoord Position,
    int MovementLeft,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Damage = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitJob? Job = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HexCoord? Destination = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TradeDealId? DealId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Exploring = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CityId? HomeCity = null) : IUnitOnMap
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
