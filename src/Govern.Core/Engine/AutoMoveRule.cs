using System.Collections.Immutable;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Engine;

/// <summary>
/// Units walking to their <see cref="Unit.Destination"/> on their own. The path is planned over what the owner knows (explored
/// terrain and rivers, bridges, units and cities only on tiles they see now), so it never reveals anything hidden, and is planned
/// again at every walk. The walk itself takes real steps while the next one fits in the movement left; a unit or city the owner
/// could not see on the way comes into the unit's sight before it is reached. The order ends, with a <see cref="UnitOrderEndedEvent"/>, when the unit arrives, when no
/// known path is left, or when a step brings an enemy unit or city into the unit's sight.
/// </summary>
internal static class AutoMoveRule
{
    // The path over the owner's knowledge, with the cost of each step; null when the target can't be planned to. Shared in meaning
    // with the view-based preview (ViewPreviewCalculator.DestinationPath): the owner's view holds exactly this knowledge.
    // A trader walking to a trade post (allowOccupiedTarget) plans through a unit it sees on the post itself: it only needs to get
    // next to it, and the real walk stops in front of the unit.
    public static ImmutableArray<(HexCoord Tile, int Cost)>? Plan(Ruleset ruleset, GameState state, Unit unit, HexCoord target, bool allowOccupiedTarget = false)
    {
        if (!state.Map.Contains(target) || target == unit.Position || state.FindPlayer(unit.Owner) is not { } owner)
        {
            return null;
        }

        var visible = VisibilityCalculator.VisibleTiles(ruleset, state, unit.Owner);
        var known = owner.Explored.ToHashSet();
        known.UnionWith(visible);
        var knowledge = new Knowledge(ruleset, state, unit.Owner, ruleset.Unit(unit.Type).Domain, known, visible, allowOccupiedTarget ? target : null);
        return PathFinder.Find(unit.Position, target, ruleset.Unit(unit.Type).Movement, knowledge.StepCost);
    }

    // Walks every one of the player's units that has a destination, in id order (at the start of the player's turn).
    public static GameState WalkAll(Ruleset ruleset, GameState state, PlayerId player, ImmutableArray<GameEvent>.Builder events) => state.Units
        .Where(unit => unit.Owner == player && unit.Destination is not null)
        .Select(static unit => unit.Id)
        .OrderBy(static id => id.Value)
        .ToList()
        .Aggregate(state, (current, id) => Walk(ruleset, current, id, events));

    // Walks one unit towards its destination as far as its movement left goes.
    public static GameState Walk(Ruleset ruleset, GameState state, UnitId unitId, ImmutableArray<GameEvent>.Builder events)
    {
        if (state.FindUnit(unitId) is not { Destination: { } destination } unit)
        {
            return state;
        }

        // A trader on a route walks by the route's rules: to a trade post, without stopping for enemies (see TradeRule).
        if (unit.RouteId is not null)
        {
            return TradeRule.Walk(ruleset, state, unit, events);
        }

        if (Plan(ruleset, state, unit, destination) is not { } path)
        {
            return End(state, unit, unit.Position == destination ? OrderEndReason.Arrived : OrderEndReason.Blocked, events);
        }

        var (walkedState, walker, walked, sighted) = TakeSteps(ruleset, state, unit, path, stopOnSighting: true);
        if (walked.Length > 0)
        {
            events.Add(new UnitMovedEvent(walker.Id, walker.Owner, walker.Type, walked));
        }

        return walker.Position == destination ? End(walkedState, walker, OrderEndReason.Arrived, events)
            : sighted ? End(walkedState, walker, OrderEndReason.EnemySighted, events)
            : walkedState;
    }

    // Takes the path's real steps while the next one fits in the movement left, stopping (when asked) after a step that brings a
    // new enemy into the unit's sight.
    public static (GameState State, Unit Unit, ImmutableArray<HexCoord> Walked, bool Sighted) TakeSteps(
        Ruleset ruleset,
        GameState state,
        Unit unit,
        ImmutableArray<(HexCoord Tile, int Cost)> path,
        bool stopOnSighting)
    {
        var enemiesBefore = EnemiesInSight(ruleset, state, unit);
        var walked = ImmutableArray.CreateBuilder<HexCoord>();
        foreach (var (tile, _) in path)
        {
            // The real step can't cost more than planned: a hidden blocker comes into the unit's sight a step earlier (sight is at
            // least 1), which ends the order below. This guard keeps the walk on the rules if that ever changes.
            if (ReachabilityCalculator.StepCost(ruleset, state, unit, unit.Position, tile) is not { } cost || cost > unit.MovementLeft)
            {
                break;
            }

            unit = unit with { Position = tile, MovementLeft = unit.MovementLeft - cost, Job = null };
            state = state.WithUnit(unit);
            walked.Add(tile);
            if (stopOnSighting && !EnemiesInSight(ruleset, state, unit).IsSubsetOf(enemiesBefore))
            {
                return (state, unit, walked.ToImmutable(), true);
            }
        }

        return (state, unit, walked.ToImmutable(), false);
    }

    private static GameState End(GameState state, Unit unit, OrderEndReason reason, ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(new UnitOrderEndedEvent(unit.Id, unit.Owner, unit.Type, unit.Position, unit.Destination!.Value, reason));
        return state.WithUnit(unit with { Destination = null });
    }

    // Tiles in the unit's own sight that hold an enemy unit or city.
    private static HashSet<HexCoord> EnemiesInSight(Ruleset ruleset, GameState state, Unit unit)
    {
        var sight = ruleset.Unit(unit.Type).Sight + ruleset.Terrain(state.Map.TileAt(unit.Position).Terrain).SightBonus;
        return unit.Position.WithinRadius(sight)
            .Where(state.Map.Contains)
            .Where(coord => state.UnitAt(coord) is { } other && other.Owner != unit.Owner || state.CityAt(coord) is { } city && city.Owner != unit.Owner)
            .ToHashSet();
    }

    // What the owner knows of the map: the tiles they explored or see, and those they see now.
    private sealed record Knowledge(Ruleset Ruleset, GameState State, PlayerId Owner, UnitDomain Domain, HashSet<HexCoord> Known, IReadOnlySet<HexCoord> Visible, HexCoord? OccupiedTargetAllowed)
    {
        // A step as the owner knows it: onto a known tile of the unit's domain, not onto a unit or enemy city they see; a river counts where
        // they know of it, and a bridge only where they see it.
        public int? StepCost(HexCoord from, HexCoord to)
        {
            if (!Known.Contains(to))
            {
                return null;
            }

            if (Visible.Contains(to) && (State.UnitAt(to) is not null && to != OccupiedTargetAllowed || State.CityAt(to) is { } city && city.Owner != Owner))
            {
                return null;
            }

            var river = HexEdge.Between(from, to) is { } edge && Known.Contains(edge.Owner)
                && !(Visible.Contains(edge.Owner) && State.Map.BridgeBetween(from, to))
                    ? State.Map.RiverBetween(from, to)
                    : null;
            return ReachabilityCalculator.StepCost(Ruleset, Domain, State.Map.TileAt(to).Terrain, river);
        }
    }
}
