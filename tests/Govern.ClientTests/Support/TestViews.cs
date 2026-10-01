using System.Collections.Immutable;
using Govern.Client.Features.Games.Services;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.ClientTests.Support;

/// <summary>
/// Player views shaped like the Api's, built by hand on a small map (all plains unless a test passes its terrain). Change the turn, status or players with
/// <c>with</c> (e.g. <c>Start() with { CurrentPlayer = Opponent }</c>).
/// </summary>
public static class TestViews
{
    public const int Width = 8;

    public const int Height = 6;

    public const long Version = 4242;

    public static readonly PlayerId You = new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000a"));

    public static readonly PlayerId Opponent = new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000b"));

    /// <summary>The client's ruleset (the default), the one <see cref="RulesetService"/> gives the components.</summary>
    public static readonly Ruleset Ruleset = new RulesetService().Ruleset;

    public static readonly UnitType SettlerType = new("settler");

    public static readonly UnitType SoldierType = new("soldier");

    public static readonly UnitType SpearmanType = new("spearman");

    public static readonly UnitType HorsemanType = new("horseman");

    public static readonly UnitType ArcherType = new("archer");

    public static readonly UnitType BuilderType = new("builder");

    public static readonly ImprovementType Farm = new("farm");

    public static readonly BuildingType Campus = new("campus");

    public static readonly BuildingType Harbour = new("harbour");

    public static readonly ResourceType Science = new("science");

    public static readonly TerrainType Plains = new("plains");

    public static readonly TerrainType Forest = new("forest");

    public static readonly TerrainType Hills = new("hills");

    public static readonly TerrainType Mountains = new("mountains");

    public static readonly RiverType StreamRiver = new("stream");

    public static readonly RiverType River = new("river");

    public static readonly RiverType DeepRiver = new("deep_river");

    /// <summary>Your settler (id 1) at (2,2), your soldier (id 2) at (3,2), and an enemy soldier (id 3) at (5,2).</summary>
    public static PlayerView Start() =>
        Create([Settler(1, You, At(2, 2)), Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(5, 2))]);

    public static PlayerView Create(
        IEnumerable<UnitView>? units = null,
        IEnumerable<CityView>? cities = null,
        Func<HexCoord, TileVisibility>? visibility = null,
        Func<HexCoord, TerrainType>? terrain = null,
        Func<HexCoord, TileRivers?>? rivers = null,
        IEnumerable<ResourceAmount>? resources = null,
        Func<HexCoord, ImprovementType?>? improvements = null,
        Func<HexCoord, TileBridges?>? bridges = null)
    {
        var tiles = Enumerable.Range(0, Width * Height)
            .Select(index => HexCoord.FromOffset(index % Width, index / Width))
            .Select(coord => (visibility?.Invoke(coord) ?? TileVisibility.Visible) switch
            {
                TileVisibility.Unexplored => new TileView(TileVisibility.Unexplored, null),
                TileVisibility.Explored => new TileView(TileVisibility.Explored, terrain?.Invoke(coord) ?? Plains, rivers?.Invoke(coord)),
                var seen => new TileView(seen, terrain?.Invoke(coord) ?? Plains, rivers?.Invoke(coord), bridges?.Invoke(coord), improvements?.Invoke(coord)),
            });

        return new PlayerView(
            You, Version, Turn: 3, CurrentPlayer: You, GameStatus.InProgress, Winner: null, Width, Height, [.. tiles],
            [.. (units ?? []).OrderBy(static unit => unit.Id.Value)],
            [.. (cities ?? []).OrderBy(static city => city.Id.Value)],
            [new PlayerSummary(You, PlayerStatus.Active), new PlayerSummary(Opponent, PlayerStatus.Active)],
            [.. resources ?? []]);
    }

    public static HexCoord At(int column, int row) => HexCoord.FromOffset(column, row);

    /// <summary>A settler; an enemy unit's movement is hidden, as in the Api's views.</summary>
    public static UnitView Settler(int id, PlayerId owner, HexCoord at, int movementLeft = 2, int health = 100) =>
        new(new UnitId(id), owner, SettlerType, at, owner == You ? movementLeft : null, health);

    /// <summary>A soldier; an enemy unit's movement is hidden, as in the Api's views.</summary>
    public static UnitView Soldier(int id, PlayerId owner, HexCoord at, int movementLeft = 2, int health = 100) =>
        new(new UnitId(id), owner, SoldierType, at, owner == You ? movementLeft : null, health);

    /// <summary>Any unit type; an enemy unit's movement is hidden, as in the Api's views.</summary>
    public static UnitView Unit(int id, PlayerId owner, UnitType type, HexCoord at, int movementLeft = 2, int health = 100) =>
        new(new UnitId(id), owner, type, at, owner == You ? movementLeft : null, health);

    /// <summary>A city; an enemy city's production is hidden, as in the Api's views.</summary>
    public static CityView City(int id, PlayerId owner, HexCoord at, string name = "City 1") =>
        owner == You
            ? new CityView(new CityId(id), owner, name, at, SettlerType, 2)
            : new CityView(new CityId(id), owner, name, at, null, null);

    public static PlayerView Finished(PlayerId? winner, PlayerStatus opponentStatus = PlayerStatus.Resigned) =>
        Start() with
        {
            Status = GameStatus.Finished,
            Winner = winner,
            Players = [new PlayerSummary(You, PlayerStatus.Active), new PlayerSummary(Opponent, opponentStatus)],
        };

    public static ImmutableArray<HexCoord> AllCoords() =>
        [.. Enumerable.Range(0, Width * Height).Select(index => HexCoord.FromOffset(index % Width, index / Width))];
}
