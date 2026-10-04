using System.Text.Json;
using System.Text.Json.Nodes;
using Govern.Core.Map;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Map;

public class PredefinedMapTests
{
    // An 8×8 map of plains with a lake tile and its mirror, a stream edge and its mirror, and two spawn candidates a side.
    private static JsonObject ValidDocument() => new()
    {
        ["key"] = "test_map",
        ["name"] = "Test map",
        ["width"] = 8,
        ["height"] = 8,
        ["legend"] = new JsonObject { ["."] = "plains", ["~"] = "lake", ["f"] = "forest" },
        ["rows"] = new JsonArray("........", "........", "...~....", "f.......", ".......f", "....~...", "........", "........"),
        ["rivers"] = new JsonArray(River(2, 4, "east", "stream"), River(4, 3, "east", "stream")),
        ["firstSpawns"] = new JsonArray(new JsonArray(1, 2), new JsonArray(1, 5)),
        ["secondSpawns"] = new JsonArray(new JsonArray(6, 5), new JsonArray(6, 2)),
    };

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
        map.FirstSpawns.ShouldBe([HexCoord.FromOffset(1, 2), HexCoord.FromOffset(1, 5)]);
        map.SecondSpawns.ShouldBe([HexCoord.FromOffset(6, 5), HexCoord.FromOffset(6, 2)]);
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
        { """{"firstSpawns":[]}""", "$.firstSpawns" },
        { """{"secondSpawns":[[6,2]]}""", "$.secondSpawns" },
        { """{"firstSpawns":[[1],[1,5]]}""", "$.firstSpawns[0]" },
        { """{"secondSpawns":[null,[6,5]]}""", "$.secondSpawns[0]" },
        { """{"rows":["........","........","...~....","f.......",".......f","....~...","........",null]}""", "$.rows[7]" },
        { """{"rivers":[null]}""", "$.rivers[0]" },
        { """{"firstSpawns":[[3,2],[1,5]]}""", "$.firstSpawns[0]" },
        { """{"firstSpawns":[[3,4],[1,5]]}""", "$.firstSpawns[0]" },
        { """{"firstSpawns":[[1,2],[1,2]]}""", "$.firstSpawns[1]" },
        { """{"secondSpawns":[[1,2],[6,2]]}""", "$.secondSpawns[0]" },
        { """{"secondSpawns":[[4,5],[6,2]]}""", "$.secondSpawns[0]" },
        { """{"spawns":[[1,2],[1,5]]}""", "$.spawns" },
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

        result.Problems.ShouldContain(problem => problem.Path == "$.firstSpawns[0]" && problem.Message.Contains("mean of all starts"), string.Join("; ", result.Problems));
    }

    [Fact]
    public void Parse_LakeWallAcrossTheMap_ReportsTheCut()
    {
        var document = ValidDocument();
        document["rows"] = new JsonArray("...~....", "...~....", "...~....", "...~....", "....~...", "....~...", "....~...", "....~...");
        document["rivers"] = new JsonArray();
        document["firstSpawns"] = new JsonArray(new JsonArray(0, 2), new JsonArray(0, 1));
        document["secondSpawns"] = new JsonArray(new JsonArray(7, 5), new JsonArray(7, 6));

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
