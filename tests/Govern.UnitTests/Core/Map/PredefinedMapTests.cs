using System.Text.Json;
using System.Text.Json.Nodes;
using Govern.Core.Map;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Map;

public class PredefinedMapTests
{
    // An 8×8 two-player map of plains with a lake tile and its mirror, a stream edge and its mirror, and two spawn candidates a
    // side.
    private static JsonObject ValidDocument() => new()
    {
        ["key"] = "test_map",
        ["name"] = "Test map",
        ["width"] = 8,
        ["height"] = 8,
        ["legend"] = new JsonObject { ["."] = "plains", ["~"] = "lake", ["f"] = "forest" },
        ["rows"] = new JsonArray("........", "........", "...~....", "f.......", ".......f", "....~...", "........", "........"),
        ["rivers"] = new JsonArray(River(2, 4, "east", "stream"), River(4, 3, "east", "stream")),
        ["spawns"] = Spawns([(1, 2), (1, 5)], [(6, 5), (6, 2)]),
    };

    private static JsonArray Spawns(params (int Column, int Row)[][] groups) =>
        [.. groups.Select(static group => (JsonNode)new JsonArray([.. group.Select(static spawn => (JsonNode)new JsonArray(spawn.Column, spawn.Row))]))];

    private static JsonObject River(int column, int row, string side, string river) =>
        new() { ["tile"] = new JsonArray(column, row), ["side"] = side, ["river"] = river };

    private static PredefinedMapParseResult Parse(JsonObject document) =>
        PredefinedMap.Parse(TestRulesets.Default, document.ToJsonString());

    [Fact]
    public void Parse_ValidDocument_BuildsTheMap()
    {
        var result = Parse(ValidDocument());

        result.Problems.ShouldBeEmpty();
        var map = result.Map!;
        map.Key.ShouldBe("test_map");
        map.Name.ShouldBe("Test map");
        map.Map.Width.ShouldBe(8);
        map.Map.TileAt(HexCoord.FromOffset(3, 2)).Terrain.ShouldBe(TestRulesets.Lake);
        map.Map.TileAt(HexCoord.FromOffset(0, 3)).Terrain.ShouldBe(TestRulesets.Forest);
        map.Map.RiverBetween(HexCoord.FromOffset(2, 4), HexCoord.FromOffset(3, 4)).ShouldBe(TestRulesets.Stream);
        map.Players.ShouldBe(2);
        map.Spawns[0].ShouldBe([HexCoord.FromOffset(1, 2), HexCoord.FromOffset(1, 5)]);
        map.Spawns[1].ShouldBe([HexCoord.FromOffset(6, 5), HexCoord.FromOffset(6, 2)]);
    }

    [Fact]
    public void Parse_EveryPoolMap_IsValidForTheDefaultRuleset()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "maps"), "*.json");

        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            var result = PredefinedMap.Parse(TestRulesets.Default, File.ReadAllText(file));
            result.Problems.ShouldBeEmpty(Path.GetFileName(file));
            result.Map!.Key.ShouldBe(Path.GetFileNameWithoutExtension(file));
        }
    }

    public static TheoryData<string, string> Broken => new()
    {
        { """{"key":"Bad Key"}""", "$.key" },
        { """{"name":" spaced"}""", "$.name" },
        { """{"width":41}""", "$.width" },
        { """{"height":41}""", "$.height" },
        { """{"legend":{".":"plains","~":"lake","f":"forest","xx":"plains"}}""", "$.legend['xx']" },
        { """{"legend":{".":"plains","~":"lake","f":"forest","s":"swamp"}}""", "$.legend['s']" },
        { """{"rows":["........","........","...~....","f.......",".......f","....~...","........"]}""", "$.rows" },
        { """{"rows":["........","........","...~....","f.......",".......f","....~...","........","......."]}""", "$.rows[7]" },
        { """{"rows":["........","........","...~....","f.......",".......f","....~...","........","...x...."]}""", "$.rows[7]" },
        { """{"rows":["ffff....","ffff....","ffff....","ffff....","ffff....","ffff....","ffff....","ffff...."]}""", "$.rows" },
        { """{"rivers":[{"tile":[7,4],"side":"east","river":"stream"},{"tile":[0,3],"side":"east","river":"stream"}]}""", "$.rivers[0]" },
        { """{"rivers":[{"tile":[2,4],"side":"east","river":"lava"}]}""", "$.rivers[0].river" },
        { """{"rivers":[{"tile":[2,4],"side":"east","river":"stream"},{"tile":[2,4],"side":"east","river":"stream"},{"tile":[4,3],"side":"east","river":"stream"}]}""", "$.rivers[1]" },
        { """{"rivers":[{"tile":[9,4],"side":"east","river":"stream"}]}""", "$.rivers[0].tile" },
        { """{"spawns":[[],[[6,5],[6,2]]]}""", "$.spawns[0]" },
        { """{"spawns":[[[1,2],[1,5]],[[6,2]]]}""", "$.spawns[1]" },
        { """{"spawns":[[[1],[1,5]],[[6,5],[6,2]]]}""", "$.spawns[0][0]" },
        { """{"spawns":[[[1,2],[1,5]],[null,[6,5]]]}""", "$.spawns[1][0]" },
        { """{"spawns":[[[1,2],[1,5]]]}""", "$.spawns" },
        { """{"spawns":[[[1,2],[1,5]],[[6,5],[6,2]],[[1,6],[1,1]],[[6,6],[6,1]],[[2,6],[2,1]]]}""", "$.spawns" },
        { """{"rows":["........","........","...~....","f.......",".......f","....~...","........",null]}""", "$.rows[7]" },
        { """{"rivers":[null]}""", "$.rivers[0]" },
        { """{"spawns":[[[3,2],[1,5]],[[6,5],[6,2]]]}""", "$.spawns[0][0]" },
        { """{"spawns":[[[3,4],[1,5]],[[6,5],[6,2]]]}""", "$.spawns[0][0]" },
        { """{"spawns":[[[1,2],[1,2]],[[6,5],[6,2]]]}""", "$.spawns[0][1]" },
        { """{"spawns":[[[1,2],[1,5]],[[1,2],[6,2]]]}""", "$.spawns[1]" },
        { """{"spawns":[[[1,2],[1,5]],[[4,5],[6,2]]]}""", "$.spawns[1][0]" },
        { """{"spawns":[[[1,2],[1,5]],[[2,3],[2,6]]]}""", "$.spawns[1][0]" },
        { """{"firstSpawns":[[1,2],[1,5]]}""", "$.firstSpawns" },
        { """{"extra":1}""", "$.extra" },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void Parse_BrokenDocument_ReportsTheProblemAtItsPath(string change, string path)
    {
        var document = ValidDocument();
        foreach (var (key, value) in JsonNode.Parse(change)!.AsObject())
        {
            document[key] = value?.DeepClone();
        }

        var result = Parse(document);

        result.IsValid.ShouldBeFalse();
        result.Problems.Select(static problem => problem.Path).ShouldContain(path, string.Join("; ", result.Problems));
    }

    [Fact]
    public void Parse_DifferentButFairHalves_IsValid()
    {
        var document = ValidDocument();
        // The east half's lake moved and a forest added on each side in different places: not a mirror, but worth the same.
        document["rows"] = new JsonArray("........", "......f.", "...~....", "f.......", ".....f..", "........", "......~.", ".f......");
        document["rivers"] = new JsonArray(River(2, 4, "east", "stream"), River(5, 1, "east", "stream"));

        var result = Parse(document);

        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_AStartFarPoorerThanTheOthers_ReportsTheSpawn()
    {
        var document = ValidDocument();
        document["rows"] = new JsonArray("dddd....", "dddd....", "dddd....", "f.......", ".......f", "....dddd", "....dddd", "....dddd");
        document["legend"] = new JsonObject { ["."] = "plains", ["~"] = "lake", ["f"] = "forest", ["d"] = "desert" };
        document["rivers"] = new JsonArray();

        var result = Parse(document);

        result.Problems.ShouldContain(problem => problem.Path == "$.spawns[0][0]" && problem.Message.Contains("mean of all starts"), string.Join("; ", result.Problems));
    }

    [Fact]
    public void Parse_OneSidesStartsRicherOnAverage_ReportsTheSides()
    {
        var document = ValidDocument();
        // Each half has 6 forest tiles, but only the east's lie within reach of its starts: every start is near the mean,
        // yet the east's average is 6.5% above the west's.
        document["width"] = 16;
        document["rows"] = new JsonArray("................", "......f.......f.", "......f......f..", "......f......f..", "......f......f..", "......f......f..", "......f.......f.", "................");
        document["rivers"] = new JsonArray();
        document["spawns"] = Spawns([(1, 2), (1, 5)], [(14, 5), (14, 2)]);

        var result = Parse(document);

        result.Problems.ShouldHaveSingleItem(string.Join("; ", result.Problems)).Path.ShouldBe("$.spawns[0]");
    }

    [Fact]
    public void Parse_ThreeGroups_IsAMapForThreePlayers()
    {
        var document = ValidDocument();
        // Plains only: the halves would differ (one start west, two east), so only the starts are compared.
        document["width"] = 12;
        document["height"] = 12;
        document["rows"] = new JsonArray([.. Enumerable.Range(0, 12).Select(static _ => (JsonNode)"............")]);
        document["rivers"] = new JsonArray();
        document["spawns"] = Spawns([(1, 4), (1, 7)], [(10, 3), (10, 4)], [(10, 7), (10, 8)]);

        var result = Parse(document);

        result.Problems.ShouldBeEmpty();
        result.Map!.Players.ShouldBe(3);
    }

    [Fact]
    public void Parse_TwoPlayersStartingRowsCanMeet_ReportsTheLaterGroup()
    {
        var document = ValidDocument();
        // Player 3's candidate [10, 3] puts its second unit on [9, 3], where player 2's candidate [9, 3] puts its first.
        document["width"] = 12;
        document["height"] = 12;
        document["rows"] = new JsonArray([.. Enumerable.Range(0, 12).Select(static _ => (JsonNode)"............")]);
        document["rivers"] = new JsonArray();
        document["spawns"] = Spawns([(1, 4), (1, 7)], [(9, 3), (10, 8)], [(10, 3), (10, 7)]);

        var result = Parse(document);

        result.Problems.ShouldContain(problem => problem.Path == "$.spawns[2]" && problem.Message.Contains("another player's starting units"), string.Join("; ", result.Problems));
        result.Problems.ShouldNotContain(problem => problem.Message.Contains("another player's starting units") && problem.Path != "$.spawns[2]");
    }

    [Fact]
    public void Parse_LakeWallAcrossTheMap_ReportsTheCut()
    {
        var document = ValidDocument();
        document["rows"] = new JsonArray("...~....", "...~....", "...~....", "...~....", "....~...", "....~...", "....~...", "....~...");
        document["rivers"] = new JsonArray();
        document["spawns"] = Spawns([(0, 2), (0, 1)], [(7, 5), (7, 6)]);

        var result = Parse(document);

        result.Problems.ShouldContain(problem => problem.Path == "$.rows" && problem.Message.Contains("can't be reached"));
    }

    [Fact]
    public void Parse_DeepRiverAcrossTheMap_ReportsTheCut()
    {
        var document = ValidDocument();
        document["rows"] = new JsonArray("........", "........", "........", "........", "........", "........", "........", "........");
        // A deep river on every edge between the west half (columns 0 to 3) and the east half; the set is its own mirror.
        var rivers = new JsonArray();
        var edges = Enumerable.Range(0, 8)
            .SelectMany(static row => Enumerable.Range(0, 4).Select(column => HexCoord.FromOffset(column, row)))
            .SelectMany(static west => west.Neighbors().Where(static east => east.Column >= 4 && east.Column < 8 && east.Row is >= 0 and < 8)
                .Select(east => HexEdge.Between(west, east)!.Value));
        foreach (var edge in edges)
        {
            rivers.Add(River(edge.Owner.Column, edge.Owner.Row, JsonNamingPolicy.CamelCase.ConvertName(edge.Side.ToString()), "deep_river"));
        }

        document["rivers"] = rivers;

        var result = Parse(document);

        result.Problems.ShouldContain(problem => problem.Path == "$.rows" && problem.Message.Contains("can't be reached"));
    }

    [Fact]
    public void ParseValid_BrokenDocument_ThrowsWithEveryProblem()
    {
        var document = ValidDocument();
        document["key"] = "Bad Key";
        document["width"] = 2;

        var exception = Should.Throw<FormatException>(() => PredefinedMap.ParseValid(TestRulesets.Default, document.ToJsonString()));

        exception.Message.ShouldContain("$.key");
        exception.Message.ShouldContain("$.width");
    }

    [Fact]
    public void Parse_NotAnObject_ReportsTheRoot()
    {
        var result = PredefinedMap.Parse(TestRulesets.Default, "null");

        result.Problems.ShouldHaveSingleItem().Path.ShouldBe("$");
    }
}
