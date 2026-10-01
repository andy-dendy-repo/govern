using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit attacked another unit. A unit brought to 0 health is reported by a following <see cref="UnitDestroyedEvent"/>.
/// A player who could see neither the attacker's tile nor owns the attacker receives the attack without
/// <paramref name="AttackerId"/> and <paramref name="From"/> (<see cref="Visibility.VisibilityCalculator.EventsFor"/>).
/// </summary>
/// <param name="AttackerId">The attacking unit; <see langword="null"/> when hidden from the receiving player.</param>
/// <param name="AttackerOwner">Its owner.</param>
/// <param name="AttackerType">Its type.</param>
/// <param name="From">Where the attacker stands; <see langword="null"/> when hidden from the receiving player.</param>
/// <param name="DefenderId">The attacked unit.</param>
/// <param name="DefenderOwner">Its owner.</param>
/// <param name="DefenderType">Its type.</param>
/// <param name="Target">Where the defender stands.</param>
/// <param name="DamageDealt">Health the defender lost.</param>
/// <param name="DamageTaken">Health the attacker lost (0 for a ranged attack or a civilian defender).</param>
public sealed record UnitAttackedEvent(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitId? AttackerId,
    PlayerId AttackerOwner,
    UnitType AttackerType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HexCoord? From,
    UnitId DefenderId,
    PlayerId DefenderOwner,
    UnitType DefenderType,
    HexCoord Target,
    int DamageDealt,
    int DamageTaken) : GameEvent;
