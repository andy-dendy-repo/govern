using System.Text.Json.Nodes;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Rulesets;

public class RulesetTests
{
    // Each change breaks one rule of the validator (or of the strict JSON reading) in the default document.
    private static readonly Dictionary<string, Action<JsonNode>> Changes = new()
    {
        ["unknown field"] = json => json["units"]![0]!["cost"] = 1,
        ["missing field"] = json => json["units"]![0]!.AsObject().Remove("sight"),
        ["number as text"] = json => json["setup"]!["players"] = "2",
        ["null section"] = json => json["cities"] = null,
        ["ability as number"] = json => json["units"]![0]!["abilities"] = new JsonArray(0),
        ["unknown ability"] = json => json["units"]![0]!["abilities"] = new JsonArray("fly"),
        ["repeated ability"] = json => json["units"]![0]!["abilities"] = new JsonArray("attack", "attack"),
        ["id not snake_case"] = json => json["units"]![0]!["id"] = "Settler",
        ["duplicate id"] = json => json["units"]![1]!["id"] = "settler",
        ["no terrains"] = json => json["terrains"] = new JsonArray(),
        ["no movement"] = json => json["units"]![1]!["movement"] = 0,
        ["free unit"] = json => json["units"]![1]!["productionCost"] = 0,
        ["free terrain"] = json => json["terrains"]![0]!["movementCost"] = 0,
        ["three players"] = json => json["setup"]!["players"] = 3,
        ["no starting units"] = json => json["setup"]!["startingUnits"] = new JsonArray(),
        ["unknown starting unit"] = json => json["setup"]!["startingUnits"] = new JsonArray("settler", "knight"),
        ["no founder"] = json => json["setup"]!["startingUnits"] = new JsonArray("soldier"),
        ["too many starting units"] = json => json["setup"]!["startingUnits"] = new JsonArray("settler", "soldier", "soldier"),
        ["map below the engine's minimum"] = json => json["map"]!["minSize"] = 7,
        ["map above the engine's maximum"] = json => json["map"]!["maxSize"] = 41,
        ["default width above the maximum"] = json => json["map"]!["defaultWidth"] = 41,
        ["unknown default terrain"] = json => json["map"]!["defaultTerrain"] = "desert",
        ["impassable default terrain"] = json => json["terrains"]![0]!["isPassable"] = false,
        ["empty spawn zone"] = json => json["map"]!["spawn"]!["zoneColumnsPerMille"] = 0,
        ["spawn zone over a quarter"] = json => json["map"]!["spawn"]!["zoneColumnsPerMille"] = 251,
        ["spawn on the edge"] = json => json["map"]!["spawn"]!["edgeMargin"] = 0,
        ["margin as wide as the zone"] = json => json["map"]!["spawn"]!["edgeMargin"] = 2,
        ["blind cities"] = json => json["cities"]!["sight"] = 0,
        ["no city spacing"] = json => json["cities"]!["minimumSpacing"] = 0,
        ["no production"] = json => json["cities"]!["productionPerTurn"] = 0,
        ["unknown initial production"] = json => json["cities"]!["initialProduction"] = "knight",
        ["missing name template"] = json => json["cities"]!["nameTemplate"] = "city.missing",
        ["template without a number"] = json => json["texts"]!["city.default_name"] = "Town",
        ["unit sees less than it moves"] = json => json["units"]![1]!["sight"] = 1,
        ["settler misses blocking cities"] = json => json["cities"]!["minimumSpacing"] = 4,
        ["missing unit name"] = json => json["texts"]!.AsObject().Remove("unit.soldier.name"),
        ["empty text"] = json => json["texts"]!["terrain.plains.name"] = " ",
        ["negative sight bonus"] = json => json["terrains"]![2]!["sightBonus"] = -1,
        ["unknown patch terrain"] = json => json["map"]!["generation"]!["patches"]![0]!["terrain"] = "desert",
        ["patch of the default terrain"] = json => json["map"]!["generation"]!["patches"]![0]!["terrain"] = "plains",
        ["terrain patched twice"] = json => json["map"]!["generation"]!["patches"]![1]!["terrain"] = "forest",
        ["empty patch share"] = json => json["map"]!["generation"]!["patches"]![0]!["sharePerMille"] = 0,
        ["empty patch"] = json => json["map"]!["generation"]!["patches"]![0]!["patchSize"] = 0,
        ["patches cover over half"] = json => json["map"]!["generation"]!["patches"]![0]!["sharePerMille"] = 400,
        ["too much impassable terrain"] = json => json["map"]!["generation"]!["patches"]![3]!["sharePerMille"] = 120,
        ["negative defence bonus"] = json => json["terrains"]![1]!["defenceBonus"] = -1,
        ["class not snake_case"] = json => json["units"]![1]!["class"] = "Melee",
        ["zero strength"] = json => json["units"]![1]!["strength"] = 0,
        ["attacker without strength"] = json => json["units"]![1]!.AsObject().Remove("strength"),
        ["melee and ranged"] = json => json["units"]![4]!["abilities"] = new JsonArray("attack", "ranged_attack"),
        ["ranged ability without ranged stats"] = json => json["units"]![4]!.AsObject().Remove("ranged"),
        ["ranged stats without the ability"] = json => json["units"]![1]!["ranged"] = new JsonObject { ["strength"] = 5, ["range"] = 1 },
        ["zero ranged strength"] = json => json["units"]![4]!["ranged"]!["strength"] = 0,
        ["range beyond sight"] = json => json["units"]![4]!["ranged"]!["range"] = 3,
        ["no health"] = json => json["combat"]!["maxHealth"] = 0,
        ["zero minimum damage"] = json => json["combat"]!["damage"]!["min"] = 0,
        ["maximum below minimum damage"] = json => json["combat"]!["damage"]!["max"] = 4,
        ["base damage above the maximum"] = json => json["combat"]!["damage"]!["base"] = 101,
        ["negative damage per strength point"] = json => json["combat"]!["damage"]!["perStrengthPoint"] = -1,
        ["spread of 100 percent"] = json => json["combat"]!["damage"]!["spreadPercent"] = 100,
        ["negative healing"] = json => json["combat"]!["healing"]!["perTurn"] = -1,
        ["negative healing in cities"] = json => json["combat"]!["healing"]!["inOwnCity"] = -1,
        ["counter for an unused class"] = json => json["combat"]!["counters"]![0]!["class"] = "siege",
        ["counter against an unused class"] = json => json["combat"]!["counters"]![0]!["against"] = "siege",
        ["counter without a bonus"] = json => json["combat"]!["counters"]![0]!["bonus"] = 0,
        ["counter listed twice"] = json => json["combat"]!["counters"]![1] = json["combat"]!["counters"]![0]!.DeepClone(),
        ["missing class name"] = json => json["texts"]!.AsObject().Remove("class.cavalry.name"),
    };

    public static TheoryData<string, string> InvalidDocuments => new()
    {
        { "unknown field", "$.units[0].cost" },
        { "missing field", "$.units[0]" },
        { "number as text", "$.setup.players" },
        { "null section", "$.cities" },
        { "ability as number", "$.units[0].abilities[0]" },
        { "unknown ability", "$.units[0].abilities[0]" },
        { "repeated ability", "$.units[0].abilities" },
        { "id not snake_case", "$.units[0].id" },
        { "duplicate id", "$.units[1].id" },
        { "no terrains", "$.terrains" },
        { "no movement", "$.units[1].movement" },
        { "free unit", "$.units[1].productionCost" },
        { "free terrain", "$.terrains[0].movementCost" },
        { "three players", "$.setup.players" },
        { "no starting units", "$.setup.startingUnits" },
        { "unknown starting unit", "$.setup.startingUnits[1]" },
        { "no founder", "$.setup.startingUnits" },
        { "too many starting units", "$.setup.startingUnits" },
        { "map below the engine's minimum", "$.map.minSize" },
        { "map above the engine's maximum", "$.map.maxSize" },
        { "default width above the maximum", "$.map.defaultWidth" },
        { "unknown default terrain", "$.map.defaultTerrain" },
        { "impassable default terrain", "$.map.defaultTerrain" },
        { "empty spawn zone", "$.map.spawn.zoneColumnsPerMille" },
        { "spawn zone over a quarter", "$.map.spawn.zoneColumnsPerMille" },
        { "spawn on the edge", "$.map.spawn.edgeMargin" },
        { "margin as wide as the zone", "$.map.spawn.edgeMargin" },
        { "blind cities", "$.cities.sight" },
        { "no city spacing", "$.cities.minimumSpacing" },
        { "no production", "$.cities.productionPerTurn" },
        { "unknown initial production", "$.cities.initialProduction" },
        { "missing name template", "$.cities.nameTemplate" },
        { "template without a number", "$.texts['city.default_name']" },
        { "unit sees less than it moves", "$.units[1].sight" },
        { "settler misses blocking cities", "$.units[0].sight" },
        { "missing unit name", "$.texts" },
        { "empty text", "$.texts['terrain.plains.name']" },
        { "negative sight bonus", "$.terrains[2].sightBonus" },
        { "unknown patch terrain", "$.map.generation.patches[0].terrain" },
        { "patch of the default terrain", "$.map.generation.patches[0].terrain" },
        { "terrain patched twice", "$.map.generation.patches[1].terrain" },
        { "empty patch share", "$.map.generation.patches[0].sharePerMille" },
        { "empty patch", "$.map.generation.patches[0].patchSize" },
        { "patches cover over half", "$.map.generation.patches" },
        { "too much impassable terrain", "$.map.generation.patches" },
        { "negative defence bonus", "$.terrains[1].defenceBonus" },
        { "class not snake_case", "$.units[1].class" },
        { "zero strength", "$.units[1].strength" },
        { "attacker without strength", "$.units[1].strength" },
        { "melee and ranged", "$.units[4].abilities" },
        { "ranged ability without ranged stats", "$.units[4].ranged" },
        { "ranged stats without the ability", "$.units[1].ranged" },
        { "zero ranged strength", "$.units[4].ranged.strength" },
        { "range beyond sight", "$.units[4].ranged.range" },
        { "no health", "$.combat.maxHealth" },
        { "zero minimum damage", "$.combat.damage.min" },
        { "maximum below minimum damage", "$.combat.damage.max" },
        { "base damage above the maximum", "$.combat.damage.base" },
        { "negative damage per strength point", "$.combat.damage.perStrengthPoint" },
        { "spread of 100 percent", "$.combat.damage.spreadPercent" },
        { "negative healing", "$.combat.healing.perTurn" },
        { "negative healing in cities", "$.combat.healing.inOwnCity" },
        { "counter for an unused class", "$.combat.counters[0].class" },
        { "counter against an unused class", "$.combat.counters[0].against" },
        { "counter without a bonus", "$.combat.counters[0].bonus" },
        { "counter listed twice", "$.combat.counters[1]" },
        { "missing class name", "$.texts" },
    };

    [Fact]
    public void Parse_DefaultRuleset_IsValidAndHoldsTodaysGame()
    {
        var result = Ruleset.Parse(TestRulesets.DefaultJson);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Problems));
        result.Problems.ShouldBeEmpty();
        var ruleset = result.Ruleset;
        ruleset.Setup.Players.ShouldBe(2);
        ruleset.Setup.StartingUnits.ShouldBe([TestRulesets.Settler, TestRulesets.Soldier]);
        ruleset.Units.Select(static unit => unit.Id).ShouldBe([TestRulesets.Settler, TestRulesets.Soldier, TestRulesets.Spearman, TestRulesets.Horseman, TestRulesets.Archer]);
        ruleset.Unit(TestRulesets.Settler).CanFoundCity.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Settler).CanAttack.ShouldBeFalse();
        ruleset.Unit(TestRulesets.Soldier).CanAttack.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Soldier).ProductionCost.ShouldBe(4);
        ruleset.Terrain(TestRulesets.Plains).ShouldBe(new TerrainDefinition(TestRulesets.Plains, 1, true, 0, 0));
        ruleset.Terrain(TestRulesets.Forest).ShouldBe(new TerrainDefinition(TestRulesets.Forest, 2, true, 0, 3));
        ruleset.Terrain(TestRulesets.Hills).ShouldBe(new TerrainDefinition(TestRulesets.Hills, 2, true, 1, 3));
        ruleset.Terrain(TestRulesets.Mountains).IsPassable.ShouldBeFalse();
        ruleset.Terrain(TestRulesets.Lake).IsPassable.ShouldBeFalse();
        ruleset.Map.Generation.Patches.Select(static patch => patch.Terrain)
            .ShouldBe([TestRulesets.Forest, TestRulesets.Hills, TestRulesets.Mountains, TestRulesets.Lake]);
        ruleset.Map.DefaultWidth.ShouldBe(20);
        ruleset.Map.DefaultHeight.ShouldBe(14);
        ruleset.Cities.MinimumSpacing.ShouldBe(3);
        ruleset.Unit(TestRulesets.Settler).Strength.ShouldBeNull();
        ruleset.Unit(TestRulesets.Archer).CanRangedAttack.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Archer).AttackRange.ShouldBe(2);
        ruleset.Unit(TestRulesets.Soldier).AttackRange.ShouldBe(1);
        ruleset.Unit(TestRulesets.Settler).AttackRange.ShouldBe(0);
        ruleset.Combat.MaxHealth.ShouldBe(100);
        ruleset.Combat.Counters.Length.ShouldBe(3);
        ruleset.ClassName("anti_cavalry").ShouldBe("Anti-cavalry");
        ruleset.Hash.ShouldBe(Ruleset.ComputeHash(TestRulesets.DefaultJson));
    }

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void Parse_InvalidDocument_ReportsTheProblemAtItsPath(string change, string path)
    {
        var result = Ruleset.Parse(TestRulesets.Document(Changes[change]).ToJsonString());

        result.IsValid.ShouldBeFalse();
        result.Ruleset.ShouldBeNull();
        result.Problems.ShouldContain(problem => problem.Path == path && problem.Message.Length > 0, string.Join("; ", result.Problems));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{ "setup": { "players": 2, "players": 2, "startingUnits": [] } }""")]
    public void Parse_NotARulesetObject_ReportsAProblem(string json)
    {
        var result = Ruleset.Parse(json);

        result.IsValid.ShouldBeFalse();
        result.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_DuplicateTextKey_ReportsAProblemInsteadOfThrowing()
    {
        var json = TestRulesets.DefaultJson.Replace(
            "\"city.default_name\": \"City {n}\"", "\"city.default_name\": \"City {n}\", \"city.default_name\": \"Town {n}\"", StringComparison.Ordinal);

        var result = Ruleset.Parse(json);

        json.ShouldNotBe(TestRulesets.DefaultJson);
        result.IsValid.ShouldBeFalse();
        result.Problems.ShouldHaveSingleItem().Path.ShouldStartWith("$.texts");
    }

    [Fact]
    public void Parse_SeveralProblems_ReportsEveryOne()
    {
        var json = TestRulesets.Document(json =>
        {
            json["units"]![1]!["movement"] = 0;
            json["cities"]!["productionPerTurn"] = 0;
        }).ToJsonString();

        var result = Ruleset.Parse(json);

        result.Problems.Select(static problem => problem.Path).ShouldBe(["$.units[1].movement", "$.cities.productionPerTurn"], ignoreOrder: true);
    }

    [Fact]
    public void ParseValid_InvalidDocument_ThrowsWithEveryProblem()
    {
        var json = TestRulesets.Document(json => json["setup"]!["players"] = 3).ToJsonString();

        var exception = Should.Throw<FormatException>(() => Ruleset.ParseValid(json));

        exception.Message.ShouldContain("$.setup.players");
    }

    [Fact]
    public void ComputeHash_AnyChangeToTheText_ChangesTheHash()
    {
        var hash = Ruleset.ComputeHash(TestRulesets.DefaultJson);

        hash.Length.ShouldBe(64);
        hash.ShouldBe(hash.ToLowerInvariant());
        Ruleset.ComputeHash(TestRulesets.DefaultJson + " ").ShouldNotBe(hash);
        Ruleset.Parse(TestRulesets.DefaultJson + "\n").Ruleset!.Hash.ShouldNotBe(hash);
    }

    [Fact]
    public void Lookups_UnknownIds_ReturnNullOrThrow()
    {
        var ruleset = TestRulesets.Default;

        ruleset.FindUnit(new UnitType("knight")).ShouldBeNull();
        ruleset.FindUnit(default).ShouldBeNull();
        Should.Throw<ArgumentException>(() => ruleset.Unit(new UnitType("knight")));
        Should.Throw<ArgumentException>(() => ruleset.Terrain(new TerrainType("desert")));
        Should.Throw<ArgumentException>(() => ruleset.Text("unit.knight.name"));
    }

    [Fact]
    public void Names_ComeFromTheTexts()
    {
        var ruleset = TestRulesets.With(json => json["texts"]!["city.default_name"] = "Town {n} of {n}");

        ruleset.UnitName(TestRulesets.Settler).ShouldBe("Settler");
        ruleset.TerrainName(TestRulesets.Plains).ShouldBe("Plains");
        ruleset.CityName(12).ShouldBe("Town 12 of 12");
    }
}
