using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Actions;

/// <summary>
/// Attacks a tile in the unit's range: fights the enemy unit on it, or captures the enemy city when no unit defends it.
/// </summary>
/// <param name="UnitId">The actor's attacking unit.</param>
/// <param name="Target">A tile in range (adjacent for melee) with an enemy unit, or an undefended enemy city for melee.</param>
public sealed record AttackCommand(UnitId UnitId, HexCoord Target) : GameCommand;

/// <summary>
/// An attack. Against a unit, both sides take damage rolled within <see cref="CombatCalculator"/>'s ranges (a ranged attacker
/// and a civilian defender take none) and a unit at 0 health is destroyed; the attacker stays where it is. An undefended enemy
/// city is captured by a melee attacker, which moves in (its production progress goes back to 0 when the ruleset says so), unless
/// a river it can't cross is in the way; the city keeps its buildings and stops producing a building. Attacking uses all remaining
/// movement and drops the attacker's job. A bridge spans its river: no river bonus, and capturing across it is allowed.
/// </summary>
internal static class AttackRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, AttackCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var attacker, out var error))
        {
            return error;
        }

        var defender = state.UnitAt(command.Target);
        var city = state.CityAt(command.Target);
        var target = new AttackTarget(
            state.Map.Contains(command.Target),
            defender?.Owner ?? city?.Owner,
            defender is not null,
            state.Map.CrossedRiverBetween(attacker.Position, command.Target),
            defender is null ? UnitDomain.Land : ruleset.Unit(defender.Type).Domain);
        if (Check(ruleset, attacker, command.Target, target) is { } violation)
        {
            return violation;
        }

        return defender is not null ? Fight(ruleset, state, attacker, defender, target.River) : Capture(ruleset, state, attacker, city!);
    }

    // Shared with the view-based previews (ViewPreviewCalculator): every tile in range is in sight (the validator keeps range
    // within sight, and sight bonuses only add), so the view holds the same units and owners there as the state.
    public static RuleViolation? Check(Ruleset ruleset, Unit attacker, HexCoord target, AttackTarget targetInfo)
    {
        var definition = ruleset.Unit(attacker.Type);
        if (definition.AttackRange == 0)
        {
            return RuleViolations.CannotAttack;
        }

        if (attacker.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        var distance = attacker.Position.DistanceTo(target);
        if (!targetInfo.OnMap || distance < 1 || distance > definition.AttackRange)
        {
            return definition.AttackRange == 1 ? RuleViolations.NotAdjacent : RuleViolations.OutOfRange;
        }

        return targetInfo.Owner switch
        {
            null => RuleViolations.NoTarget,
            var owner when owner == attacker.Owner => RuleViolations.OwnTarget,
            _ when definition.CanRangedAttack && !targetInfo.HasUnit => RuleViolations.NoTarget,
            _ when !definition.CanRangedAttack && targetInfo.Domain != definition.Domain => RuleViolations.OtherDomain,
            _ when !targetInfo.HasUnit && targetInfo.River is { } river && !ruleset.River(river).IsCrossable => RuleViolations.RiverInTheWay,
            _ => null,
        };
    }

    public static Combatant CombatantOf(GameState state, Ruleset ruleset, Unit unit) =>
        new(unit.Type, unit.Health(ruleset), state.Map.TileAt(unit.Position).Terrain);

    private static GameUpdate Fight(Ruleset ruleset, GameState state, Unit attacker, Unit defender, RiverType? river)
    {
        var forecast = CombatCalculator.Forecast(ruleset, CombatantOf(state, ruleset, attacker), CombatantOf(state, ruleset, defender), river);
        var random = new DeterministicRandom(state.RngState);
        var dealt = Roll(ruleset, random, forecast.Defender, forecast.Attacker);
        var taken = Roll(ruleset, random, forecast.Attacker, forecast.Defender);

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        events.Add(new UnitAttackedEvent(
            attacker.Id, attacker.Owner, attacker.Type, attacker.Position, defender.Id, defender.Owner, defender.Type, defender.Position, dealt, taken));
        var next = Hurt(ruleset, state with { RngState = random.State }, defender, dealt, events);
        next = Hurt(ruleset, next, attacker with { MovementLeft = 0, Job = null, Destination = null, Exploring = false }, taken, events);
        return new GameUpdate(next, events.ToImmutable());
    }

    // The damage one side takes: fixed when its range is a single value (none, or sure destruction), otherwise rolled. The
    // roll uses NextHashed: damage is shown to both players, and raw draws would expose the generator's state.
    private static int Roll(Ruleset ruleset, DeterministicRandom random, CombatSide receiver, CombatSide dealer)
    {
        if (receiver.MinDamage == receiver.MaxDamage)
        {
            return receiver.MinDamage;
        }

        var (min, maxExclusive) = CombatCalculator.RollRange(ruleset.Combat.Damage);
        var raw = CombatCalculator.RawDamage(ruleset.Combat.Damage, dealer.Strength, receiver.Strength);
        return CombatCalculator.Damage(raw, random.NextHashed(min, maxExclusive), receiver.Health);
    }

    // Applies damage; a unit brought to 0 health is removed.
    private static GameState Hurt(Ruleset ruleset, GameState state, Unit unit, int damage, ImmutableArray<GameEvent>.Builder events)
    {
        if (damage < unit.Health(ruleset))
        {
            return state.WithUnit(unit with { Damage = unit.Damage + damage });
        }

        events.Add(new UnitDestroyedEvent(unit.Id, unit.Owner, unit.Type, unit.Position));
        return state.WithoutUnit(unit.Id);
    }

    private static GameUpdate Capture(Ruleset ruleset, GameState state, Unit attacker, City city)
    {
        var progress = ruleset.Cities.CaptureResetsProduction ? 0 : city.ProductionProgress;
        var producing = ScienceCalculator.NextProduction(ruleset, state.FindPlayer(attacker.Owner)!.NodeList, city.Producing);
        var next = state
            .WithCity(city with { Owner = attacker.Owner, Producing = producing, ProductionProgress = progress, ProducingBuilding = null, ProducingBuildingSite = null, ProductionPaid = false })
            .WithUnit(attacker with { Position = city.Position, MovementLeft = 0, Job = null, Destination = null, Exploring = false });

        return new GameUpdate(next, [
            new UnitMovedEvent(attacker.Id, attacker.Owner, attacker.Type, [city.Position]),
            new CityCapturedEvent(city.Id, city.Name, city.Position, city.Owner, attacker.Owner, attacker.Id),
        ]);
    }
}
