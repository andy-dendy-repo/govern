using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit attacked another unit. A unit brought to 0 health is reported by a following <see cref="UnitDestroyedEvent"/>.
/// </summary>
/// <param name="AttackerId">The attacking unit.</param>
/// <param name="AttackerOwner">Its owner.</param>
/// <param name="From">Where the attacker stands.</param>
/// <param name="DefenderId">The attacked unit.</param>
/// <param name="DefenderOwner">Its owner.</param>
/// <param name="Target">Where the defender stands.</param>
/// <param name="DamageDealt">Health the defender lost.</param>
/// <param name="DamageTaken">Health the attacker lost (0 for a ranged attack or a civilian defender).</param>
public sealed record UnitAttackedEvent(
    UnitId AttackerId,
    PlayerId AttackerOwner,
    HexCoord From,
    UnitId DefenderId,
    PlayerId DefenderOwner,
    HexCoord Target,
    int DamageDealt,
    int DamageTaken) : GameEvent;
