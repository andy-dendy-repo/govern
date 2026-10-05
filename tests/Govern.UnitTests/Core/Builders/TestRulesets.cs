using System.Text.Json.Nodes;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.UnitTests.Core.Builders;

/// <summary>
/// The real default ruleset (<c>rulesets/default/v13.json</c>, copied next to the tests) and variants of it for rules whose data
/// matters to a test.
/// </summary>
internal static class TestRulesets
{
    public static readonly string DefaultJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rulesets", "default", "v13.json"));

    public static readonly Ruleset Default = Ruleset.ParseValid(DefaultJson);

    public static readonly UnitType Settler = new("settler");
    public static readonly UnitType Soldier = new("soldier");
    public static readonly UnitType Spearman = new("spearman");
    public static readonly UnitType Horseman = new("horseman");
    public static readonly UnitType Archer = new("archer");
    public static readonly UnitType Builder = new("builder");
    public static readonly UnitType Scout = new("scout");
    public static readonly UnitType Trader = new("trader");
    public static readonly UnitType Swordsman = new("swordsman");
    public static readonly UnitType Pikeman = new("pikeman");
    public static readonly UnitType Crossbowman = new("crossbowman");
    public static readonly UnitType Knight = new("knight");
    public static readonly UnitType Galley = new("galley");
    public static readonly UnitType Bireme = new("bireme");
    public static readonly TerrainType Plains = new("plains");
    public static readonly TerrainType Forest = new("forest");
    public static readonly TerrainType Hills = new("hills");
    public static readonly TerrainType Mountains = new("mountains");
    public static readonly TerrainType Lake = new("lake");
    public static readonly TerrainType Desert = new("desert");
    public static readonly RiverType Stream = new("stream");
    public static readonly RiverType River = new("river");
    public static readonly RiverType DeepRiver = new("deep_river");
    public static readonly ResourceType Food = new("food");
    public static readonly ResourceType Water = new("water");
    public static readonly ResourceType Wood = new("wood");
    public static readonly ResourceType Stone = new("stone");
    public static readonly ResourceType Gold = new("gold");
    public static readonly ResourceType Science = new("science");
    public static readonly ResourceType Culture = new("culture");
    public static readonly ImprovementType Farm = new("farm");
    public static readonly ImprovementType LumberCamp = new("lumber_camp");
    public static readonly ImprovementType Mine = new("mine");
    public static readonly BuildingType ArmyCamp = new("army_camp");
    public static readonly BuildingType Campus = new("campus");
    public static readonly BuildingType TradePost = new("trade_post");
    public static readonly BuildingType CultureCentre = new("culture_centre");
    public static readonly BuildingType Harbour = new("harbour");
    public static readonly BuildingType Workshop = new("workshop");
    public static readonly BuildingType Barracks = new("barracks");
    public static readonly BuildingType University = new("university");
    public static readonly BuildingType Market = new("market");
    public static readonly BuildingType Port = new("port");

    // The default document after a change, e.g. With(json => json["cities"]!["productionPerTurn"] = 2).
    public static JsonNode Document(Action<JsonNode>? change = null)
    {
        var document = JsonNode.Parse(DefaultJson)!;
        change?.Invoke(document);
        return document;
    }

    public static Ruleset With(Action<JsonNode> change) => Ruleset.ParseValid(Document(change).ToJsonString());

    // Adds a unit, with its name text, to a document: a melee unit of strength 20 when it can attack, a civilian otherwise.
    public static void AddUnit(JsonNode document, string id, int movement, int sight, int cost, params string[] abilities)
    {
        document["units"]!.AsArray().Add(new JsonObject
        {
            ["id"] = id,
            ["class"] = abilities.Contains("attack") ? "melee" : "civilian",
            ["movement"] = movement,
            ["sight"] = sight,
            ["productionCost"] = cost,
            ["resourceCost"] = new JsonArray(),
            ["upkeep"] = new JsonArray(),
            ["abilities"] = new JsonArray([.. abilities.Select(static ability => (JsonNode)ability)]),
        });
        if (abilities.Contains("attack"))
        {
            document["units"]!.AsArray()[^1]!["strength"] = 20;
        }

        document["texts"]![$"unit.{id}.name"] = id;
    }
}
