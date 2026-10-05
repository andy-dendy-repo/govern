using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.State;

/// <summary>
/// Complete, immutable snapshot of one game. Only <c>GameEngine.Execute</c> produces new states; the host stores it (as JSON)
/// and never edits it. Players see a filtered <c>PlayerView</c>, never this object.
/// </summary>
public sealed record GameState
{
    /// <summary>
    /// Version of this shape; bump it (and migrate stored games in <c>GameSerializer.DeserializeState</c>) on breaking changes.
    /// Version 2 removed the shared id counter: ids are drawn from <see cref="RngState"/>. Version 3 moved resources from the
    /// players to their cities and gave cities territory (<see cref="Map.Tile.Territory"/>).
    /// </summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Schema version the snapshot was written with.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Number of commands applied so far; increases by one per successful command.</summary>
    public required long Version { get; init; }

    /// <summary>Round number, starting at 1; increases when the turn passes back to the first player.</summary>
    public required int Turn { get; init; }

    /// <summary>Player who may act now.</summary>
    public required PlayerId CurrentPlayer { get; init; }

    /// <summary>Whether the game is still running.</summary>
    public required GameStatus Status { get; init; }

    /// <summary>The winner once <see cref="Status"/> is <see cref="GameStatus.Finished"/>; otherwise <see langword="null"/>.</summary>
    public PlayerId? Winner { get; init; }

    /// <summary>The map.</summary>
    public required GameMap Map { get; init; }

    /// <summary>Players in turn order.</summary>
    public required ImmutableArray<Player> Players { get; init; }

    /// <summary>Every unit on the map.</summary>
    public required ImmutableArray<Unit> Units { get; init; }

    /// <summary>Every city on the map.</summary>
    public required ImmutableArray<City> Cities { get; init; }

    /// <summary>
    /// Every trade deal (G17b), in the order they were proposed; <see langword="null"/> while there are none. Games saved before
    /// trading read as having none, and games saved with G17's trade routes drop them (the old <c>tradeRoutes</c> member is
    /// skipped), so no schema change.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<TradeDeal>? TradeDeals { get; init; }

    /// <summary>Every trade deal (empty when there are none).</summary>
    [JsonIgnore]
    public ImmutableArray<TradeDeal> TradeDealList => TradeDeals ?? [];

    /// <summary>
    /// State of the <see cref="DeterministicRandom"/> generator. It also draws unit and city ids, so an id tells an opponent
    /// nothing about how many units or cities were created. Never sent to clients.
    /// </summary>
    public required ulong RngState { get; init; }

    /// <summary>Returns the unit with the given id.</summary>
    /// <param name="id">Unit id.</param>
    /// <returns>The unit, or <see langword="null"/> when it does not exist (any more).</returns>
    public Unit? FindUnit(UnitId id) => Units.FirstOrDefault(unit => unit.Id == id);

    /// <summary>
    /// Returns the unit holding a tile: the one unit on it that doesn't share tiles (traders do, see
    /// <see cref="UnitDefinition.SharesTiles"/>).
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="coord">The tile.</param>
    /// <returns>The unit, or <see langword="null"/> when no such unit stands there (traders may).</returns>
    public Unit? HolderAt(Ruleset ruleset, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return Units.FirstOrDefault(unit => unit.Position == coord && !ruleset.Unit(unit.Type).SharesTiles);
    }

    /// <summary>Returns every unit standing on a tile: its holder, if any, and the traders sharing it.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The units, in the state's order (empty when none).</returns>
    public IEnumerable<Unit> UnitsAt(HexCoord coord) => Units.Where(unit => unit.Position == coord);

    /// <summary>Returns the city with the given id.</summary>
    /// <param name="id">City id.</param>
    /// <returns>The city, or <see langword="null"/> when it does not exist.</returns>
    public City? FindCity(CityId id) => Cities.FirstOrDefault(city => city.Id == id);

    /// <summary>Returns the city on a tile.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The city, or <see langword="null"/> when the tile has none.</returns>
    public City? CityAt(HexCoord coord) => Cities.FirstOrDefault(city => city.Position == coord);

    /// <summary>Returns a participant.</summary>
    /// <param name="id">Player id.</param>
    /// <returns>The player, or <see langword="null"/> when they are not part of this game.</returns>
    public Player? FindPlayer(PlayerId id) => Players.FirstOrDefault(player => player.Id == id);

    internal GameState WithUnit(Unit unit) =>
        this with { Units = Units.Replace(FindUnit(unit.Id) ?? throw MissingUnit(unit.Id), unit) };

    internal GameState WithoutUnit(UnitId id) =>
        this with { Units = Units.Remove(FindUnit(id) ?? throw MissingUnit(id)) };

    internal GameState WithCity(City city) =>
        this with { Cities = Cities.Replace(FindCity(city.Id) ?? throw new InvalidOperationException($"City {city.Id.Value} does not exist."), city) };

    /// <summary>Returns a trade deal.</summary>
    /// <param name="id">Deal id.</param>
    /// <returns>The deal, or <see langword="null"/> when it does not exist (any more).</returns>
    public TradeDeal? FindDeal(TradeDealId id) => TradeDealList.FirstOrDefault(deal => deal.Id == id);

    internal GameState WithDeal(TradeDeal deal) => FindDeal(deal.Id) is { } old
        ? this with { TradeDeals = TradeDealList.Replace(old, deal) }
        : this with { TradeDeals = TradeDealList.Add(deal) };

    internal GameState WithoutDeal(TradeDealId id)
    {
        var deals = TradeDealList.RemoveAll(deal => deal.Id == id);
        return this with { TradeDeals = deals.IsEmpty ? null : deals };
    }

    internal GameState WithPlayer(Player player) =>
        this with { Players = Players.Replace(FindPlayer(player.Id) ?? throw new InvalidOperationException("The player is not part of the game."), player) };

    // A random id not used by any unit or city; drawn from the game's generator, so replays give the same ids.
    internal (GameState State, int Id) AllocateId()
    {
        var random = new DeterministicRandom(RngState);
        var id = NewId(random, IsIdInUse);
        return (this with { RngState = random.State }, id);
    }

    // Ids are shown to opponents, so they never carry raw generator output: SplitMix64 is invertible, and two raw draws would
    // let a player rebuild RngState (and with it hidden spawns and every future id). A SHA-256 of the draw keeps the id
    // deterministic but reveals nothing about the state.
    internal static int NewId(DeterministicRandom random, Func<int, bool> isInUse)
    {
        Span<byte> draw = stackalloc byte[sizeof(ulong)];
        int id;
        do
        {
            BinaryPrimitives.WriteUInt64LittleEndian(draw, random.NextUInt64());
            id = (int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(draw)) & int.MaxValue);
        }
        while (id == 0 || isInUse(id));

        return id;
    }

    private bool IsIdInUse(int id) =>
        Units.Any(unit => unit.Id.Value == id) || Cities.Any(city => city.Id.Value == id) || TradeDealList.Any(deal => deal.Id.Value == id);

    private static InvalidOperationException MissingUnit(UnitId id) => new($"Unit {id.Value} does not exist.");
}
