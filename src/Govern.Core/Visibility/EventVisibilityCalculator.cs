using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

// Filters a command's events for one player (VisibilityCalculator.EventsFor). A player keeps every event about their own
// units and cities, every event every player sees (turns, resignations, the result), and another player's event only where
// it happened on a tile they could see when the command began or after it ended. Events about another player's private
// state (production, jobs, upkeep) never pass.
internal static class EventVisibilityCalculator
{
    public static ImmutableArray<GameEvent> EventsFor(Ruleset ruleset, GameState before, GameUpdate update, PlayerId viewer)
    {
        var seen = VisibilityCalculator.VisibleTiles(ruleset, before, viewer).ToHashSet();
        seen.UnionWith(VisibilityCalculator.VisibleTiles(ruleset, update.State, viewer));
        var owners = new Owners(before, update.State);

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        foreach (var gameEvent in update.Events)
        {
            if (Filter(gameEvent, viewer, seen, owners) is { } visible)
            {
                events.Add(visible);
            }
        }

        return events.ToImmutable();
    }

    private static GameEvent? Filter(GameEvent gameEvent, PlayerId viewer, HashSet<HexCoord> seen, Owners owners) => gameEvent switch
    {
        TurnEndedEvent or PlayerResignedEvent or PlayerEliminatedEvent or GameWonEvent => gameEvent,
        UnitMovedEvent moved => Moved(moved, viewer, seen),
        UnitAttackedEvent attacked => Attacked(attacked, viewer, seen),
        CityCapturedEvent captured => captured.PreviousOwner == viewer || captured.NewOwner == viewer || seen.Contains(captured.Position)
            ? captured
            : null,
        BridgeBuiltEvent bridge => bridge.Owner == viewer || seen.Contains(bridge.From) || seen.Contains(bridge.To) ? bridge : null,
        _ when Place(gameEvent) is var (owner, tile) => owner == viewer || seen.Contains(tile) ? gameEvent : null,
        _ => Private(gameEvent, viewer, owners),
    };

    // The owner and tile of an event seen where it happened; null for the other kinds.
    private static (PlayerId Owner, HexCoord Tile)? Place(GameEvent gameEvent) => gameEvent switch
    {
        UnitDestroyedEvent destroyed => (destroyed.Owner, destroyed.Position),
        CityFoundedEvent founded => (founded.Owner, founded.Position),
        UnitCreatedEvent created => (created.Owner, created.Position),
        ImprovementBuiltEvent built => (built.Owner, built.Position),
        TerritoryGrewEvent grew => (grew.Owner, grew.Tile),
        _ => null,
    };

    // Events about a player's private state reach only that player. A new event type is hidden until it is given a row in
    // one of these switches (EventVisibilityTests checks that every type has one).
    private static GameEvent? Private(GameEvent gameEvent, PlayerId viewer, Owners owners)
    {
        var owner = gameEvent switch
        {
            ProductionChangedEvent changed => owners.OfCity(changed.CityId),
            BuildingCompletedEvent completed => owners.OfCity(completed.CityId),
            BuildingDamagedEvent damaged => owners.OfCity(damaged.CityId),
            UpkeepPrioritySetEvent priority => owners.OfCity(priority.CityId),
            BuildStartedEvent started => owners.OfUnit(started.UnitId),
            UnitStarvedEvent starved => owners.OfUnit(starved.UnitId),
            _ => null,
        };

        return owner == viewer ? gameEvent : null;
    }

    // The opponent sees the part of the path on tiles they could see; the unit's owner sees all of it.
    private static UnitMovedEvent? Moved(UnitMovedEvent moved, PlayerId viewer, HashSet<HexCoord> seen)
    {
        if (moved.Owner == viewer)
        {
            return moved;
        }

        var path = moved.Path.Where(seen.Contains).ToImmutableArray();
        return path.IsEmpty ? null : moved with { Path = path };
    }

    // A fight is seen when either tile is, or one of the units is the viewer's; an attacker on a tile the viewer couldn't see
    // keeps its owner and type (the defender knows what hit it) but not its id or tile.
    private static UnitAttackedEvent? Attacked(UnitAttackedEvent attacked, PlayerId viewer, HashSet<HexCoord> seen)
    {
        var attackerSeen = attacked.AttackerOwner == viewer || attacked.From is { } from && seen.Contains(from);
        var defenderSeen = attacked.DefenderOwner == viewer || seen.Contains(attacked.Target);
        if (!attackerSeen && !defenderSeen)
        {
            return null;
        }

        return attackerSeen ? attacked : attacked with { AttackerId = null, From = null };
    }

    // Who owns a unit or city named by an event: after the command, or before it for one that is gone.
    private sealed class Owners(GameState before, GameState after)
    {
        public PlayerId? OfCity(CityId id) =>
            after.Cities.FirstOrDefault(city => city.Id == id)?.Owner ?? before.Cities.FirstOrDefault(city => city.Id == id)?.Owner;

        public PlayerId? OfUnit(UnitId id) =>
            after.Units.FirstOrDefault(unit => unit.Id == id)?.Owner ?? before.Units.FirstOrDefault(unit => unit.Id == id)?.Owner;
    }
}
