using Govern.Client.Features.Games.Models;
using Govern.Client.Helpers;
using Govern.Core.Cities;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Writes the game log's events as sentences for the side panel. The player is "you"; another player is named by seat
/// ("Seat 2"), the colour the board uses for them. Names come from the ruleset and the current view.
/// </summary>
public static class GameLogFormatter
{
    /// <summary>Describes one event.</summary>
    /// <param name="ruleset">The game's ruleset (unit, improvement and building names).</param>
    /// <param name="view">The player's current view (seats, own city and unit names).</param>
    /// <param name="gameEvent">An event from the player's log.</param>
    /// <returns>The line; <see langword="null"/> for an event type the log doesn't show.</returns>
    public static GameLogLine? Describe(Ruleset ruleset, PlayerView view, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);

        var names = new Names(ruleset, view);
        return Units(names, gameEvent) ?? Cities(names, gameEvent) ?? Game(names, gameEvent);
    }

    private static GameLogLine? Units(Names names, GameEvent gameEvent) => gameEvent switch
    {
        UnitMovedEvent moved => new($"{names.Unit(moved.Owner, moved.UnitType)} moved", moved.Path[^1], names.IsYou(moved.Owner)),
        UnitAttackedEvent attacked => Attacked(names, attacked),
        UnitDestroyedEvent destroyed => new($"{names.Unit(destroyed.Owner, destroyed.UnitType)} was destroyed", destroyed.Position, names.IsYou(destroyed.Owner)),
        UnitCreatedEvent created => new($"{names.Player(created.Owner)} trained {Article(names.UnitType(created.UnitType))}", created.Position, names.IsYou(created.Owner)),
        UnitStarvedEvent starved => new($"{names.OwnUnit(starved.UnitId)} is short of supplies: {starved.Health} health left", names.UnitTile(starved.UnitId), true),
        BuildStartedEvent started => new($"{names.OwnUnit(started.UnitId)} started {names.Job(started.Job)}", names.UnitTile(started.UnitId), true),
        ImprovementBuiltEvent built => new($"{names.Player(built.Owner)} built {Article(names.Improvement(built))}", built.Position, names.IsYou(built.Owner)),
        BridgeBuiltEvent bridge => new($"{names.Player(bridge.Owner)} built a bridge", bridge.From, names.IsYou(bridge.Owner)),
        _ => null,
    };

    private static GameLogLine Attacked(Names names, UnitAttackedEvent attacked)
    {
        var attacker = attacked.AttackerId is null
            ? $"An unseen {names.UnitOf(attacked.AttackerOwner, attacked.AttackerType)}"
            : names.Unit(attacked.AttackerOwner, attacked.AttackerType);
        var taken = attacked.DamageTaken > 0 ? $", {attacked.DamageTaken} taken" : string.Empty;
        var text = $"{attacker} hit {names.Unit(attacked.DefenderOwner, attacked.DefenderType, lower: true)}: {attacked.DamageDealt} damage{taken}";
        return new(text, attacked.Target, names.IsYou(attacked.AttackerOwner));
    }

    private static GameLogLine? Cities(Names names, GameEvent gameEvent) => gameEvent switch
    {
        CityFoundedEvent founded => new($"{names.Player(founded.Owner)} founded {founded.Name}", founded.Position, names.IsYou(founded.Owner)),
        CityCapturedEvent captured => new(
            $"{names.Player(captured.NewOwner)} captured {captured.Name} from {names.Player(captured.PreviousOwner, lower: true)}",
            captured.Position,
            names.IsYou(captured.NewOwner)),
        TerritoryGrewEvent grew => new($"{names.City(grew.CityId, grew.Owner)} grew its territory", grew.Tile, names.IsYou(grew.Owner)),
        ProductionChangedEvent changed => new($"{names.OwnCity(changed.CityId)} now builds {names.Production(changed)}", names.CityTile(changed.CityId), true),
        BuildingCompletedEvent completed => new($"{names.OwnCity(completed.CityId)} finished {Article(names.Building(completed.Building))}", names.CityTile(completed.CityId), true),
        BuildingDamagedEvent damaged => new(
            $"{names.OwnCity(damaged.CityId)}'s {names.Building(damaged.Building)} is short of upkeep: {damaged.Health} health left",
            names.CityTile(damaged.CityId),
            true),
        UpkeepPrioritySetEvent priority => new($"{names.OwnCity(priority.CityId)} now pays for {Priority(priority.Priority)} first", names.CityTile(priority.CityId), true),
        _ => null,
    };

    private static GameLogLine? Game(Names names, GameEvent gameEvent) => gameEvent switch
    {
        TurnEndedEvent ended => new(
            names.IsYou(ended.NextPlayer) ? $"Turn {ended.Turn}: your turn" : $"Turn {ended.Turn}: {names.Player(ended.NextPlayer)}'s turn",
            null,
            names.IsYou(ended.PreviousPlayer)),
        PlayerResignedEvent resigned => new($"{names.Player(resigned.PlayerId)} resigned", null, names.IsYou(resigned.PlayerId)),
        PlayerEliminatedEvent eliminated => new($"{names.Player(eliminated.PlayerId)} {(names.IsYou(eliminated.PlayerId) ? "were" : "was")} eliminated", null, names.IsYou(eliminated.PlayerId)),
        GameWonEvent won => new($"{names.Player(won.Winner)} won the game", null, names.IsYou(won.Winner)),
        _ => null,
    };

    /// <summary>Puts "a" or "an" before a name, by its first letter.</summary>
    /// <param name="name">A unit, improvement or building name.</param>
    /// <returns>The name with its article, such as "an Archer".</returns>
    public static string Article(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length > 0 && "AEIOUaeiou".Contains(name[0], StringComparison.Ordinal) ? $"an {name}" : $"a {name}";
    }

    private static string Priority(UpkeepPriority priority) => priority == UpkeepPriority.UnitsFirst ? "units" : "buildings";

    // Names as one player reads them.
    private sealed class Names(Ruleset ruleset, PlayerView view)
    {
        public bool IsYou(PlayerId player) => player == view.Viewer;

        public string Player(PlayerId player, bool lower = false) =>
            IsYou(player) ? (lower ? "you" : "You") : $"Seat {Seats.Number(view, player)}";

        public string UnitType(UnitType type) => ruleset.UnitName(type);

        // "Your Soldier", "Seat 2's Soldier" (or "your Soldier" inside a sentence).
        public string Unit(PlayerId owner, UnitType type, bool lower = false) =>
            IsYou(owner) ? $"{(lower ? "your" : "Your")} {UnitType(type)}" : $"Seat {Seats.Number(view, owner)}'s {UnitType(type)}";

        // "Seat 2 Archer" (after "An unseen").
        public string UnitOf(PlayerId owner, UnitType type) =>
            IsYou(owner) ? $"{UnitType(type)} of yours" : $"Seat {Seats.Number(view, owner)} {UnitType(type)}";

        public string OwnUnit(UnitId id) =>
            view.Units.FirstOrDefault(unit => unit.Id == id) is { } unit ? $"Your {UnitType(unit.Type)}" : "One of your units";

        public Core.Map.HexCoord? UnitTile(UnitId id) => view.Units.FirstOrDefault(unit => unit.Id == id)?.Position;

        public string OwnCity(CityId id) => view.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? "One of your cities";

        public string City(CityId id, PlayerId owner) =>
            view.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? (IsYou(owner) ? "One of your cities" : $"A city of Seat {Seats.Number(view, owner)}");

        public Core.Map.HexCoord? CityTile(CityId id) => view.Cities.FirstOrDefault(city => city.Id == id)?.Position;

        public string Improvement(ImprovementBuiltEvent built) => ruleset.ImprovementName(built.Improvement);

        public string Building(Core.Economy.BuildingType building) => ruleset.BuildingName(building);

        public string Job(UnitJob job) => job.Improvement is { } improvement ? Article(ruleset.ImprovementName(improvement)) : "a bridge";

        public string Production(ProductionChangedEvent changed) =>
            changed.Building is { } building ? Article(Building(building)) : $"{UnitType(changed.UnitType)} units";
    }
}
