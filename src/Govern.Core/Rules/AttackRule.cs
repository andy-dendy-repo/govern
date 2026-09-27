using Govern.Core.Cities;
using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.Players;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Rules;

/// <summary>
/// Melee attack on an adjacent tile. v1 combat is deterministic: the attacker always wins. A defending unit is destroyed;
/// an undefended enemy city is captured and the attacker moves in. Attacking uses all remaining movement.
/// </summary>
internal static class AttackRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, AttackCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var attacker, out var error))
        {
            return error;
        }

        if (Check(state, attacker, command) is { } violation)
        {
            return violation;
        }

        if (state.UnitAt(command.Target) is { } defender)
        {
            return defender.Owner == actor ? GameErrors.OwnTarget : Destroy(state, attacker, defender);
        }

        if (state.CityAt(command.Target) is { } city)
        {
            return city.Owner == actor ? GameErrors.OwnTarget : Capture(state, attacker, city);
        }

        return GameErrors.NoTarget;
    }

    private static GameError? Check(GameState state, Unit attacker, AttackCommand command)
    {
        if (!attacker.Definition.CanAttack)
        {
            return GameErrors.CannotAttack;
        }

        if (attacker.MovementLeft <= 0)
        {
            return GameErrors.NoMovementLeft;
        }

        return state.Map.Contains(command.Target) && attacker.Position.DistanceTo(command.Target) == 1
            ? null
            : GameErrors.NotAdjacent;
    }

    private static CommandResult Destroy(GameState state, Unit attacker, Unit defender)
    {
        var next = state.WithoutUnit(defender.Id).WithUnit(attacker with { MovementLeft = 0 });
        return new GameUpdate(next, [new UnitDestroyedEvent(defender.Id, defender.Owner, defender.Position)]);
    }

    private static CommandResult Capture(GameState state, Unit attacker, City city)
    {
        var next = state
            .WithCity(city with { Owner = attacker.Owner, ProductionProgress = 0 })
            .WithUnit(attacker with { Position = city.Position, MovementLeft = 0 });

        return new GameUpdate(next, [
            new UnitMovedEvent(attacker.Id, [city.Position]),
            new CityCapturedEvent(city.Id, city.Owner, attacker.Owner, attacker.Id),
        ]);
    }
}
