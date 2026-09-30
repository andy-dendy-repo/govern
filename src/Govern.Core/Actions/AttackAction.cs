using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Attacks an adjacent tile: destroys the enemy unit on it, or captures the enemy city when no unit defends it.
/// </summary>
/// <param name="UnitId">The actor's attacking unit.</param>
/// <param name="Target">Adjacent tile with an enemy unit or city.</param>
public sealed record AttackCommand(UnitId UnitId, HexCoord Target) : GameCommand;

/// <summary>
/// Melee attack on an adjacent tile. v1 combat is deterministic: the attacker always wins. A defending unit is destroyed;
/// an undefended enemy city is captured and the attacker moves in (its production progress goes back to 0 when the ruleset says
/// so). Attacking uses all remaining movement.
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
        if (Check(ruleset, attacker, command.Target, state.Map.Contains(command.Target), defender?.Owner ?? city?.Owner) is { } violation)
        {
            return violation;
        }

        return defender is not null ? Destroy(state, attacker, defender) : Capture(ruleset, state, attacker, city!);
    }

    // Shared with the view-based preview (ViewPreviewCalculator): everything it inspects is adjacent to the attacker, so it is
    // visible and the view holds the same owners as the state. The target's owner is the defending unit's, else the city's.
    public static RuleViolation? Check(Ruleset ruleset, Unit attacker, HexCoord target, bool targetOnMap, PlayerId? targetOwner)
    {
        if (!ruleset.Unit(attacker.Type).CanAttack)
        {
            return RuleViolations.CannotAttack;
        }

        if (attacker.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        if (!targetOnMap || attacker.Position.DistanceTo(target) != 1)
        {
            return RuleViolations.NotAdjacent;
        }

        return targetOwner switch
        {
            null => RuleViolations.NoTarget,
            var owner when owner == attacker.Owner => RuleViolations.OwnTarget,
            _ => null,
        };
    }

    private static CommandResult Destroy(GameState state, Unit attacker, Unit defender)
    {
        var next = state.WithoutUnit(defender.Id).WithUnit(attacker with { MovementLeft = 0 });
        return new GameUpdate(next, [new UnitDestroyedEvent(defender.Id, defender.Owner, defender.Position)]);
    }

    private static CommandResult Capture(Ruleset ruleset, GameState state, Unit attacker, City city)
    {
        var progress = ruleset.Cities.CaptureResetsProduction ? 0 : city.ProductionProgress;
        var next = state
            .WithCity(city with { Owner = attacker.Owner, ProductionProgress = progress })
            .WithUnit(attacker with { Position = city.Position, MovementLeft = 0 });

        return new GameUpdate(next, [
            new UnitMovedEvent(attacker.Id, [city.Position]),
            new CityCapturedEvent(city.Id, city.Owner, attacker.Owner, attacker.Id),
        ]);
    }
}
