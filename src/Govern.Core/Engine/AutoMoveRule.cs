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
/// could not see stops it for this turn. The order ends, with a <see cref="UnitOrderEndedEvent"/>, when the unit arrives, when no
/// known path is left, or when a step brings an enemy unit or city into the unit's sight.
/// </summary>
internal static class AutoMoveRule
{
    // The path over the owner's knowledge, with the cost of each step; null when the target can't be planned to. Shared in meaning
    // with the view-based preview (ViewPreviewCalculator.DestinationPath): the owner's view holds exactly this knowledge.
    public static ImmutableArray<(HexCoord Tile, int Cost)>? Plan(Ruleset ruleset, GameState state, Unit unit, HexCoord target)
    {
        if (!state.Map.Contains(target) || target == unit.Position || state.FindPlayer(unit.Owner) is not { } owner)
        {
            return null;
        }

        var visible = VisibilityCalculator.VisibleTiles(ruleset, state, unit.Owner);
        var known = owner.Explored.ToHashSet();
        known.UnionWith(visible);
        return PathFinder.Find(unit.Position, target, ruleset.Unit(unit.Type).Movement, (from, to) => KnownStepCost(ruleset, state, unit.Owner, known, visible, from, to));
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

        if (Plan(ruleset, state, unit, destination) is not { } path)
        {
            return End(state, unit, unit.Position == destination ? OrderEndReason.Arrived : OrderEndReason.Blocked, events);
        }

        var enemiesBefore = EnemiesInSight(ruleset, state, unit);
        var walked = new List<HexCoord>();
        var sighted = false;
        foreach (var (tile, _) in path)
        {
            if (ReachabilityCalculator.StepCost(ruleset, state, unit, unit.Position, tile) is not { } cost || cost > unit.MovementLeft)
            {
                break;
            }

            unit = unit with { Position = tile, MovementLeft = unit.MovementLeft - cost, Job = null };
            state = state.WithUnit(unit);
            walked.Add(tile);
            if (!EnemiesInSight(ruleset, state, unit).IsSubsetOf(enemiesBefore))
            {
                sighted = true;
                break;
            }
        }

        if (walked.Count > 0)
        {
            events.Add(new UnitMovedEvent(unit.Id, unit.Owner, unit.Type, [.. walked]));
        }

        return unit.Position == destination ? End(state, unit, OrderEndReason.Arrived, events)
            : sighted ? End(state, unit, OrderEndReason.EnemySighted, events)
            : state;
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

    // A step as the owner knows it: onto a known passable tile, not onto a unit or enemy city they see; a river counts where they
    // know of it, and a bridge only where they see it.
    private static int? KnownStepCost(
        Ruleset ruleset,
        GameState state,
        PlayerId owner,
        HashSet<HexCoord> known,
        IReadOnlySet<HexCoord> visible,
        HexCoord from,
        HexCoord to)
    {
        if (!known.Contains(to) || !state.Map.IsPassable(ruleset, to))
        {
            return null;
        }

        if (visible.Contains(to) && (state.UnitAt(to) is not null || state.CityAt(to) is { } city && city.Owner != owner))
        {
            return null;
        }

        var river = HexEdge.Between(from, to) is { } edge && known.Contains(edge.Owner)
            && !(visible.Contains(edge.Owner) && state.Map.BridgeBetween(from, to))
                ? state.Map.RiverBetween(from, to)
                : null;
        return ReachabilityCalculator.StepCost(ruleset, state.Map.TileAt(to).Terrain, river);
    }
}
