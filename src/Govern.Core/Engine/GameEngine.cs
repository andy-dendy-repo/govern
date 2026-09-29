using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Engine;

/// <summary>
/// The only entry point that changes a game. Pure and deterministic: the same state, actor and command always give the same
/// result, so the server can validate authoritatively and the client can preview with the same code.
/// </summary>
public static class GameEngine
{
    /// <summary>
    /// Validates and applies a command. After the command's own rule, eliminations and victory are checked, every player's
    /// explored tiles are updated and <see cref="GameState.Version"/> increases by one.
    /// </summary>
    /// <param name="state">Current state.</param>
    /// <param name="actor">Player who sent the command.</param>
    /// <param name="command">The command.</param>
    /// <returns>The update, or the <see cref="RuleViolation"/> that rejected the command (the state is then unchanged).</returns>
    public static CommandResult Execute(GameState state, PlayerId actor, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (CommandGuardRule.CheckTurn(state, actor, command) is { } error)
        {
            return error;
        }

        var result = Dispatch(state, actor, command);
        return result.IsSuccess ? Complete(result.Update) : result;
    }

    /// <summary>Tiles one of the viewer's units can move to this turn, for highlighting in the UI.</summary>
    /// <param name="state">Current state.</param>
    /// <param name="viewer">Player asking; other players' units always give an empty result, so hidden units stay hidden.</param>
    /// <param name="unitId">The unit.</param>
    /// <returns>Reachable tiles ordered by row then column; empty when the viewer has no such unit or it cannot move.</returns>
    public static ImmutableArray<HexCoord> ReachableTiles(GameState state, PlayerId viewer, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.FindUnit(unitId) is not { MovementLeft: > 0 } unit || unit.Owner != viewer)
        {
            return [];
        }

        return [.. ReachabilityCalculator.Compute(state, unit).Destinations
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)];
    }

    /// <summary>
    /// Tiles one of the viewer's units can move to now, worked out from their view alone (for the client). A tile is in the
    /// result exactly when a <see cref="MoveUnitCommand"/> to it would be accepted.
    /// </summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The unit.</param>
    /// <returns>
    /// Reachable tiles ordered by row then column; empty when it is not the viewer's turn, the game is over, or the viewer has no
    /// such unit or it cannot move.
    /// </returns>
    public static ImmutableArray<HexCoord> ReachableTiles(PlayerView view, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return ViewPreviewCalculator.ReachableTiles(view, unitId);
    }

    /// <summary>
    /// Tiles one of the viewer's units can attack now, worked out from their view alone (for the client). A tile is in the
    /// result exactly when an <see cref="AttackCommand"/> on it would be accepted.
    /// </summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The unit.</param>
    /// <returns>Adjacent tiles with an enemy unit or city, ordered by row then column; empty when the unit cannot attack now.</returns>
    public static ImmutableArray<HexCoord> AttackTargets(PlayerView view, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return ViewPreviewCalculator.AttackTargets(view, unitId);
    }

    /// <summary>
    /// Whether one of the viewer's units can found a city now, worked out from their view alone (for the client).
    /// </summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The settler.</param>
    /// <returns>
    /// <see langword="null"/> when a <see cref="FoundCityCommand"/> would be accepted; otherwise the violation it would be
    /// rejected with.
    /// </returns>
    public static RuleViolation? CheckFoundCity(PlayerView view, UnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return ViewPreviewCalculator.CheckFoundCity(view, unitId);
    }

    /// <summary>What one participant may see of the game (fog of war applied).</summary>
    /// <param name="state">Current state.</param>
    /// <param name="viewer">A participant.</param>
    /// <returns>The filtered view.</returns>
    /// <exception cref="ArgumentException"><paramref name="viewer"/> is not part of the game.</exception>
    public static PlayerView ViewFor(GameState state, PlayerId viewer) => VisibilityCalculator.ViewFor(state, viewer);

    private static CommandResult Dispatch(GameState state, PlayerId actor, GameCommand command) => command switch
    {
        MoveUnitCommand move => MoveUnitRule.Apply(state, actor, move),
        AttackCommand attack => AttackRule.Apply(state, actor, attack),
        FoundCityCommand found => FoundCityRule.Apply(state, actor, found),
        SetProductionCommand production => SetProductionRule.Apply(state, actor, production),
        EndTurnCommand => EndTurnRule.Apply(state, actor),
        ResignCommand => ResignRule.Apply(state, actor),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unknown command."),
    };

    private static GameUpdate Complete(GameUpdate update)
    {
        var decided = VictoryRule.Apply(update);
        var explored = VisibilityCalculator.UpdateExplored(decided.State);
        return decided with { State = explored with { Version = explored.Version + 1 } };
    }
}
