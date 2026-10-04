using System.Text.Json.Nodes;
using Govern.Core.Economy;
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
        ["unknown starting unit"] = json => json["setup"]!["startingUnits"] = new JsonArray("settler", "dragoon"),
        ["no founder"] = json => json["setup"]!["startingUnits"] = new JsonArray("soldier"),
        ["too many starting units"] = json => json["setup"]!["startingUnits"] = new JsonArray("settler", "soldier", "soldier"),
        ["map below the engine's minimum"] = json => json["map"]!["minSize"] = 7,
        ["map above the engine's maximum"] = json => json["map"]!["maxSize"] = 41,
        ["default width above the maximum"] = json => json["map"]!["defaultWidth"] = 41,
        ["unknown default terrain"] = json => json["map"]!["defaultTerrain"] = "tundra",
        ["impassable default terrain"] = json => json["terrains"]![0]!["isPassable"] = false,
        ["empty spawn zone"] = json => json["map"]!["spawn"]!["zoneColumnsPerMille"] = 0,
        ["spawn zone over a quarter"] = json => json["map"]!["spawn"]!["zoneColumnsPerMille"] = 251,
        ["spawn on the edge"] = json => json["map"]!["spawn"]!["edgeMargin"] = 0,
        ["margin as wide as the zone"] = json => json["map"]!["spawn"]!["edgeMargin"] = 2,
        ["blind cities"] = json => json["cities"]!["sight"] = 0,
        ["no city spacing"] = json => json["cities"]!["minimumSpacing"] = 0,
        ["no production"] = json => json["cities"]!["productionPerTurn"] = 0,
        ["unknown initial production"] = json => json["cities"]!["initialProduction"] = "dragoon",
        ["no suggested city names"] = json => json["cities"]!["suggestedNames"] = new JsonArray(),
        ["missing suggested city name"] = json => json["cities"]!["suggestedNames"]![0] = "city.missing",
        ["invalid suggested city name"] = json => json["texts"]!["city.name.ambermoor"] = "<Amber>",
        ["untrimmed suggested city name"] = json => json["texts"]!["city.name.ambermoor"] = " Ambermoor",
        ["suggested city name used twice"] = json => json["texts"]!["city.name.brightwater"] = "AMBERMOOR",
        ["unit sees less than it moves"] = json => json["units"]![1]!["sight"] = 1,
        ["settler misses blocking cities"] = json => json["cities"]!["minimumSpacing"] = 4,
        ["missing unit name"] = json => json["texts"]!.AsObject().Remove("unit.soldier.name"),
        ["empty text"] = json => json["texts"]!["terrain.plains.name"] = " ",
        ["negative sight bonus"] = json => json["terrains"]![2]!["sightBonus"] = -1,
        ["unknown patch terrain"] = json => json["map"]!["generation"]!["patches"]![0]!["terrain"] = "tundra",
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
        ["defence bonus too big"] = json => json["terrains"]![1]!["defenceBonus"] = 101,
        ["strength too big"] = json => json["units"]![1]!["strength"] = 1001,
        ["ranged strength too big"] = json => json["units"]![4]!["ranged"]!["strength"] = 1001,
        ["health too big"] = json => json["combat"]!["maxHealth"] = 1001,
        ["maximum damage too big"] = json => json["combat"]!["damage"]!["max"] = 1001,
        ["damage per strength point too big"] = json => json["combat"]!["damage"]!["perStrengthPoint"] = 101,
        ["healing too big"] = json => json["combat"]!["healing"]!["perTurn"] = 1001,
        ["counter bonus too big"] = json => json["combat"]!["counters"]![0]!["bonus"] = 101,
        ["no rivers"] = json => json["rivers"] = new JsonArray(),
        ["river id not snake_case"] = json => json["rivers"]![0]!["id"] = "Stream",
        ["negative crossing cost"] = json => json["rivers"]![1]!["crossingCost"] = -1,
        ["crossing cost too big"] = json => json["rivers"]![1]!["crossingCost"] = 101,
        ["negative river defence bonus"] = json => json["rivers"]![1]!["defenceBonus"] = -1,
        ["river defence bonus too big"] = json => json["rivers"]![1]!["defenceBonus"] = 101,
        ["no crossable river"] = json => { foreach (var river in json["rivers"]!.AsArray()) { river!["isCrossable"] = false; } },
        ["missing river name"] = json => json["texts"]!.AsObject().Remove("river.deep_river.name"),
        ["negative river count"] = json => json["map"]!["generation"]!["rivers"]!["count"] = -1,
        ["too many rivers"] = json => json["map"]!["generation"]!["rivers"]!["count"] = 21,
        ["empty river"] = json => json["map"]!["generation"]!["rivers"]!["minLength"] = 0,
        ["maximum river length below the minimum"] = json => json["map"]!["generation"]!["rivers"]!["maxLength"] = 3,
        ["river too long"] = json => json["map"]!["generation"]!["rivers"]!["maxLength"] = 101,
        ["no river sources"] = json => json["map"]!["generation"]!["rivers"]!["sources"] = new JsonArray(),
        ["unknown river source"] = json => json["map"]!["generation"]!["rivers"]!["sources"] = new JsonArray("hills", "tundra"),
        ["river source listed twice"] = json => json["map"]!["generation"]!["rivers"]!["sources"] = new JsonArray("hills", "hills"),
        ["unknown river mouth"] = json => json["map"]!["generation"]!["rivers"]!["mouths"] = new JsonArray("sea"),
        ["no river stages"] = json => json["map"]!["generation"]!["rivers"]!["stages"] = new JsonArray(),
        ["unknown stage river"] = json => json["map"]!["generation"]!["rivers"]!["stages"]![1]!["river"] = "canal",
        ["first stage after the source"] = json => json["map"]!["generation"]!["rivers"]!["stages"]![0]!["fromSegment"] = 1,
        ["stages out of order"] = json => json["map"]!["generation"]!["rivers"]!["stages"]![2]!["fromSegment"] = 3,
        ["uncrossable first stage"] = json => json["map"]!["generation"]!["rivers"]!["stages"]![0]!["river"] = "deep_river",
        ["no resources"] = json => json["resources"] = new JsonArray(),
        ["resource listed twice"] = json => json["resources"]![1]!["id"] = "food",
        ["missing resource name"] = json => json["texts"]!.AsObject().Remove("resource.gold.name"),
        ["improvement id not snake_case"] = json => json["improvements"]![0]!["id"] = "Farm",
        ["free improvement"] = json => json["improvements"]![0]!["cost"] = 0,
        ["improvement without terrains"] = json => json["improvements"]![0]!["terrains"] = new JsonArray(),
        ["unknown improvement terrain"] = json => json["improvements"]![0]!["terrains"] = new JsonArray("plains", "tundra"),
        ["improvement terrain listed twice"] = json => json["improvements"]![0]!["terrains"] = new JsonArray("plains", "plains"),
        ["improvement on impassable terrain"] = json => json["improvements"]![2]!["terrains"] = new JsonArray("mountains"),
        ["unknown yield resource"] = json => json["improvements"]![0]!["yields"]![0]!["resource"] = "iron",
        ["empty yield"] = json => json["improvements"]![1]!["yields"]![0]!["amount"] = 0,
        ["yield resource listed twice"] = json => json["buildings"]![1]!["yields"] = new JsonArray(
            new JsonObject { ["resource"] = "science", ["amount"] = 1 }, new JsonObject { ["resource"] = "science", ["amount"] = 1 }),
        ["missing improvement name"] = json => json["texts"]!.AsObject().Remove("improvement.mine.name"),
        ["free building"] = json => json["buildings"]![0]!["cost"] = 0,
        ["negative building production"] = json => json["buildings"]![0]!["production"] = -1,
        ["unknown adjacent terrain"] = json => json["buildings"]![4]!["requiresAdjacentTerrain"] = new JsonArray("sea"),
        ["missing building name"] = json => json["texts"]!.AsObject().Remove("building.harbour.name"),
        ["free bridge"] = json => json["bridges"]!["cost"] = 0,
        ["builder without build production"] = json => json["units"]![5]!.AsObject().Remove("buildProduction"),
        ["build production without an ability"] = json => json["units"]![2]!["buildProduction"] = 1,
        ["unknown terrain yield resource"] = json => json["terrains"]![0]!["yields"]![0]!["resource"] = "iron",
        ["empty river yield"] = json => json["rivers"]![0]!["yields"]![0]!["amount"] = 0,
        ["unknown city yield resource"] = json => json["cities"]!["yields"]![0]!["resource"] = "iron",
        ["starting stock too large"] = json => json["cities"]!["startingStock"]![0]!["amount"] = 1001,
        ["negative initial territory"] = json => json["territory"]!["initialRadius"] = -1,
        ["overlapping new territories"] = json => json["territory"]!["initialRadius"] = 2,
        ["maximum territory below the initial one"] = json => json["territory"]!["maxRadius"] = 0,
        ["territory too large"] = json => json["territory"]!["maxRadius"] = 11,
        ["unknown growth resource"] = json => json["territory"]!["growthResource"] = "faith",
        ["free territory"] = json => json["territory"]!["tileCost"] = 0,
        ["negative territory price increase"] = json => json["territory"]!["tileCostIncrease"] = -1,
        ["unit cost of an unknown resource"] = json => json["units"]![1]!["resourceCost"]![0]!["resource"] = "iron",
        ["unit cost too high"] = json => json["units"]![1]!["resourceCost"]![0]!["amount"] = 1001,
        ["unit upkeep of zero"] = json => json["units"]![1]!["upkeep"]![0]!["amount"] = 0,
        ["unit cost in culture"] = json => json["units"]![1]!["resourceCost"]![0]!["resource"] = "culture",
        ["building upkeep listed twice"] = json => json["buildings"]![0]!["upkeep"] = JsonNode.Parse("""[{ "resource": "food", "amount": 1 }, { "resource": "food", "amount": 2 }]"""),
        ["building cost of an unknown resource"] = json => json["buildings"]![0]!["resourceCost"]![0]!["resource"] = "iron",
        ["supply radius too large"] = json => json["upkeep"]!["supplyRadius"] = 4,
        ["supply beyond sight"] = json => json["upkeep"]!["supplyRadius"] = 2,
        ["no shortage damage"] = json => json["upkeep"]!["shortageDamage"] = 0,
        ["shortage damage kills"] = json => json["upkeep"]!["shortageDamage"] = 100,
        ["buildings without health"] = json => json["upkeep"]!["buildingHealth"] = 0,
        ["building recovery above its health"] = json => json["upkeep"]!["buildingRecovery"] = 101,
        ["negative trade value"] = json => json["resources"]![0]!["tradeValue"] = -1,
        ["trade value too high"] = json => json["resources"]![0]!["tradeValue"] = 101,
        ["tradable culture"] = json => json["resources"]![6]!["tradeValue"] = 1,
        ["unknown trade building"] = json => json["trade"]!["building"] = "bazaar",
        ["no trade routes"] = json => json["trade"]!["routesPerCity"] = 0,
        ["no market cap"] = json => json["trade"]!["maxCap"] = 0,
        ["market cap too high"] = json => json["trade"]!["maxCap"] = 101,
        ["no demand step"] = json => json["trade"]!["demandStepPercent"] = 0,
        ["no demand smoothing"] = json => json["trade"]!["demandSmoothing"] = 0,
        ["lowest demand above base"] = json => json["trade"]!["minDemandPercent"] = 101,
        ["highest demand below base"] = json => json["trade"]!["maxDemandPercent"] = 99,
        ["missing trade section"] = json => json.AsObject().Remove("trade"),
        ["trader that attacks"] = json => json["units"]![7]!["abilities"]!.AsArray().Add("attack"),
        ["nothing tradable"] = json =>
        {
            foreach (var resource in json["resources"]!.AsArray())
            {
                resource!.AsObject().Remove("tradeValue");
            }
        },
        ["missing science section"] = json => json.AsObject().Remove("science"),
        ["unknown science resource"] = json => json["science"]!["resource"] = "mana",
        ["tradable science"] = json => json["resources"]![5]!["tradeValue"] = 1,
        ["science as a cost"] = json => json["buildings"]![0]!["resourceCost"]!.AsArray().Add(new JsonObject { ["resource"] = "science", ["amount"] = 1 }),
        ["node id not snake_case"] = json => json["science"]!["nodes"]![0]!["id"] = "Archery",
        ["node without points"] = json => json["science"]!["nodes"]![0]!["points"] = 0,
        ["node requires a later node"] = json => json["science"]!["nodes"]![0]!["requires"] = new JsonArray("iron_working"),
        ["node requires an unknown node"] = json => json["science"]!["nodes"]![6]!["requires"] = new JsonArray("alchemy"),
        ["node unlocks an unknown unit"] = json => json["science"]!["nodes"]![0]!["units"] = new JsonArray("dragoon"),
        ["node unlocks an unknown building"] = json => json["science"]!["nodes"]![3]!["buildings"] = new JsonArray("lighthouse"),
        ["unit unlocked by two nodes"] = json => json["science"]!["nodes"]![1]!["units"] = new JsonArray("horseman", "archer"),
        ["node locks a starting unit"] = json => json["science"]!["nodes"]![0]!["units"] = new JsonArray("archer", "settler"),
        ["missing node name"] = json => json["texts"]!.AsObject().Remove("science.archery.name"),
        ["unit replaces an unknown unit"] = json => json["units"]![8]!["replaces"] = "legionary",
        ["unit replaces another class"] = json => json["units"]![8]!["replaces"] = "archer",
        ["two units replace one"] = json => json["units"]![9]!["replaces"] = "soldier",
        ["replacing unit without an upgrade cost"] = json => json["units"]![8]!.AsObject().Remove("upgradeCost"),
        ["upgrade cost without a replaced unit"] = json => json["units"]![1]!["upgradeCost"] = new JsonArray(),
        ["units replace each other"] = json => json["units"]![1]!["replaces"] = "swordsman",
        ["building upgrades itself"] = json => json["buildings"]![6]!["upgrades"] = "barracks",
        ["two buildings upgrade one"] = json => json["buildings"]![8]!["upgrades"] = "campus",
        ["passable water"] = json => json["terrains"]![4]!["isPassable"] = true,
        ["unknown production building"] = json => json["units"]![12]!["requiresBuilding"] = "dock",
        ["ship without its building"] = json => json["units"]![12]!.AsObject().Remove("requiresBuilding"),
        ["ship launched from an inland building"] = json => json["units"]![12]!["requiresBuilding"] = "campus",
        ["ship that founds cities"] = json => json["units"]![12]!["abilities"]!.AsArray().Add("found_city"),
        ["ship without water"] = json => json["terrains"]![4]!["isWater"] = false,
        ["starting ship"] = json => json["setup"]!["startingUnits"]!.AsArray().Add("galley"),
        ["first production needs a building"] = json => json["cities"]!["initialProduction"] = "galley",
        ["ship sees less than it sails"] = json => json["units"]![12]!["sight"] = 2,
        ["trade building is an upgrade"] = json => json["trade"]!["building"] = "market",
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
        { "no suggested city names", "$.cities.suggestedNames" },
        { "missing suggested city name", "$.cities.suggestedNames[0]" },
        { "invalid suggested city name", "$.texts['city.name.ambermoor']" },
        { "untrimmed suggested city name", "$.texts['city.name.ambermoor']" },
        { "suggested city name used twice", "$.texts['city.name.brightwater']" },
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
        { "defence bonus too big", "$.terrains[1].defenceBonus" },
        { "strength too big", "$.units[1].strength" },
        { "ranged strength too big", "$.units[4].ranged.strength" },
        { "health too big", "$.combat.maxHealth" },
        { "maximum damage too big", "$.combat.damage.max" },
        { "damage per strength point too big", "$.combat.damage.perStrengthPoint" },
        { "healing too big", "$.combat.healing.perTurn" },
        { "counter bonus too big", "$.combat.counters[0].bonus" },
        { "no rivers", "$.rivers" },
        { "river id not snake_case", "$.rivers[0].id" },
        { "negative crossing cost", "$.rivers[1].crossingCost" },
        { "crossing cost too big", "$.rivers[1].crossingCost" },
        { "negative river defence bonus", "$.rivers[1].defenceBonus" },
        { "river defence bonus too big", "$.rivers[1].defenceBonus" },
        { "no crossable river", "$.rivers" },
        { "missing river name", "$.texts" },
        { "negative river count", "$.map.generation.rivers.count" },
        { "too many rivers", "$.map.generation.rivers.count" },
        { "empty river", "$.map.generation.rivers.minLength" },
        { "maximum river length below the minimum", "$.map.generation.rivers.maxLength" },
        { "river too long", "$.map.generation.rivers.maxLength" },
        { "no river sources", "$.map.generation.rivers.sources" },
        { "unknown river source", "$.map.generation.rivers.sources[1]" },
        { "river source listed twice", "$.map.generation.rivers.sources" },
        { "unknown river mouth", "$.map.generation.rivers.mouths[0]" },
        { "no river stages", "$.map.generation.rivers.stages" },
        { "unknown stage river", "$.map.generation.rivers.stages[1].river" },
        { "first stage after the source", "$.map.generation.rivers.stages[0].fromSegment" },
        { "stages out of order", "$.map.generation.rivers.stages[2].fromSegment" },
        { "uncrossable first stage", "$.map.generation.rivers.stages[0].river" },
        { "no resources", "$.resources" },
        { "resource listed twice", "$.resources[1].id" },
        { "missing resource name", "$.texts" },
        { "improvement id not snake_case", "$.improvements[0].id" },
        { "free improvement", "$.improvements[0].cost" },
        { "improvement without terrains", "$.improvements[0].terrains" },
        { "unknown improvement terrain", "$.improvements[0].terrains[1]" },
        { "improvement terrain listed twice", "$.improvements[0].terrains[1]" },
        { "improvement on impassable terrain", "$.improvements[2].terrains" },
        { "unknown yield resource", "$.improvements[0].yields[0].resource" },
        { "empty yield", "$.improvements[1].yields[0].amount" },
        { "yield resource listed twice", "$.buildings[1].yields[1].resource" },
        { "missing improvement name", "$.texts" },
        { "free building", "$.buildings[0].cost" },
        { "negative building production", "$.buildings[0].production" },
        { "unknown adjacent terrain", "$.buildings[4].requiresAdjacentTerrain[0]" },
        { "missing building name", "$.texts" },
        { "free bridge", "$.bridges.cost" },
        { "builder without build production", "$.units[5].buildProduction" },
        { "build production without an ability", "$.units[2].buildProduction" },
        { "unknown terrain yield resource", "$.terrains[0].yields[0].resource" },
        { "empty river yield", "$.rivers[0].yields[0].amount" },
        { "unknown city yield resource", "$.cities.yields[0].resource" },
        { "starting stock too large", "$.cities.startingStock[0].amount" },
        { "negative initial territory", "$.territory.initialRadius" },
        { "overlapping new territories", "$.territory.initialRadius" },
        { "maximum territory below the initial one", "$.territory.maxRadius" },
        { "territory too large", "$.territory.maxRadius" },
        { "unknown growth resource", "$.territory.growthResource" },
        { "free territory", "$.territory.tileCost" },
        { "negative territory price increase", "$.territory.tileCostIncrease" },
        { "unit cost of an unknown resource", "$.units[1].resourceCost[0].resource" },
        { "unit cost too high", "$.units[1].resourceCost[0].amount" },
        { "unit upkeep of zero", "$.units[1].upkeep[0].amount" },
        { "unit cost in culture", "$.units[1].resourceCost" },
        { "building upkeep listed twice", "$.buildings[0].upkeep[1].resource" },
        { "building cost of an unknown resource", "$.buildings[0].resourceCost[0].resource" },
        { "supply radius too large", "$.upkeep.supplyRadius" },
        { "supply beyond sight", "$.units[0].sight" },
        { "no shortage damage", "$.upkeep.shortageDamage" },
        { "shortage damage kills", "$.upkeep.shortageDamage" },
        { "buildings without health", "$.upkeep.buildingHealth" },
        { "building recovery above its health", "$.upkeep.buildingRecovery" },
        { "negative trade value", "$.resources[0].tradeValue" },
        { "trade value too high", "$.resources[0].tradeValue" },
        { "tradable culture", "$.resources[6].tradeValue" },
        { "unknown trade building", "$.trade.building" },
        { "no trade routes", "$.trade.routesPerCity" },
        { "no market cap", "$.trade.maxCap" },
        { "market cap too high", "$.trade.maxCap" },
        { "no demand step", "$.trade.demandStepPercent" },
        { "no demand smoothing", "$.trade.demandSmoothing" },
        { "lowest demand above base", "$.trade.minDemandPercent" },
        { "highest demand below base", "$.trade.maxDemandPercent" },
        { "missing trade section", "$" },
        { "trader that attacks", "$.units[7].abilities" },
        { "nothing tradable", "$.resources" },
        { "missing science section", "$" },
        { "unknown science resource", "$.science.resource" },
        { "tradable science", "$.science.resource" },
        { "science as a cost", "$.science.resource" },
        { "node id not snake_case", "$.science.nodes[0].id" },
        { "node without points", "$.science.nodes[0].points" },
        { "node requires a later node", "$.science.nodes[0].requires" },
        { "node requires an unknown node", "$.science.nodes[6].requires" },
        { "node unlocks an unknown unit", "$.science.nodes[0].units" },
        { "node unlocks an unknown building", "$.science.nodes[3].buildings" },
        { "unit unlocked by two nodes", "$.science.nodes[1].units" },
        { "node locks a starting unit", "$.science.nodes" },
        { "missing node name", "$.texts" },
        { "unit replaces an unknown unit", "$.units[8].replaces" },
        { "unit replaces another class", "$.units[8].replaces" },
        { "two units replace one", "$.units[9].replaces" },
        { "replacing unit without an upgrade cost", "$.units[8].upgradeCost" },
        { "upgrade cost without a replaced unit", "$.units[1].upgradeCost" },
        { "units replace each other", "$.units" },
        { "building upgrades itself", "$.buildings[6].upgrades" },
        { "two buildings upgrade one", "$.buildings[8].upgrades" },
        { "passable water", "$.terrains[4].isWater" },
        { "unknown production building", "$.units[12].requiresBuilding" },
        { "ship without its building", "$.units[12].requiresBuilding" },
        { "ship launched from an inland building", "$.units[12].requiresBuilding" },
        { "ship that founds cities", "$.units[12].abilities" },
        { "ship without water", "$.units[12].domain" },
        { "starting ship", "$.setup.startingUnits[2]" },
        { "first production needs a building", "$.cities.initialProduction" },
        { "ship sees less than it sails", "$.units[12].sight" },
        { "trade building is an upgrade", "$.trade.building" },
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
        ruleset.Units.Select(static unit => unit.Id).ShouldBe([TestRulesets.Settler, TestRulesets.Soldier, TestRulesets.Spearman, TestRulesets.Horseman, TestRulesets.Archer, TestRulesets.Builder, TestRulesets.Scout, TestRulesets.Trader, TestRulesets.Swordsman, TestRulesets.Pikeman, TestRulesets.Crossbowman, TestRulesets.Knight, TestRulesets.Galley, TestRulesets.Bireme]);
        ruleset.ReplacementOf(TestRulesets.Soldier)!.Id.ShouldBe(TestRulesets.Swordsman);
        ruleset.ReplacementOf(TestRulesets.Archer)!.Id.ShouldBe(TestRulesets.Crossbowman);
        ruleset.ReplacementOf(TestRulesets.Swordsman).ShouldBeNull();
        ruleset.Unit(TestRulesets.Knight).UpgradeCost.ShouldBe([new ResourceAmount(TestRulesets.Stone, 3)]);
        ruleset.Science.Resource.ShouldBe(TestRulesets.Science);
        ruleset.Science.Nodes.Select(static node => node.Id).ShouldBe(["archery", "horseback_riding", "trade", "sailing", "bronze_working", "education", "iron_working", "machinery", "stirrups", "currency", "shipbuilding"]);
        ruleset.NodeUnlocking(TestRulesets.Trader)!.Id.ShouldBe("trade");
        ruleset.NodeUnlocking(TestRulesets.TradePost)!.Id.ShouldBe("trade");
        ruleset.NodeUnlocking(TestRulesets.Soldier).ShouldBeNull();
        ruleset.FindNode("iron_working")!.Requires.ShouldBe(["bronze_working"]);
        ruleset.UpgradeOf(TestRulesets.Campus)!.Id.ShouldBe(TestRulesets.University);
        ruleset.IsKindOf(TestRulesets.Market, TestRulesets.TradePost).ShouldBeTrue();
        ruleset.IsKindOf(TestRulesets.TradePost, TestRulesets.Market).ShouldBeFalse();
        ruleset.Unit(TestRulesets.Trader).ShouldSatisfyAllConditions(
            static trader => trader.CanTrade.ShouldBeTrue(),
            static trader => trader.Strength.ShouldBeNull(),
            static trader => trader.Movement.ShouldBe(3));
        ruleset.TradableResources.Select(static resource => (resource.Id.Id, resource.TradeValue))
            .ShouldBe([("food", 1), ("wood", 2), ("stone", 2), ("gold", 3)]);
        ruleset.Trade.ShouldBe(new TradeRules(TestRulesets.TradePost, 2, 20, 20, 5, 25, 400));
        ruleset.Unit(TestRulesets.Builder).CanBuildImprovements.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Builder).CanBuildBridges.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Builder).BuildProduction.ShouldBe(2);
        ruleset.Unit(TestRulesets.Soldier).CanBuildImprovements.ShouldBeFalse();
        ruleset.Unit(TestRulesets.Soldier).CanBuildBridges.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Soldier).BuildProduction.ShouldBe(1);
        ruleset.Unit(TestRulesets.Archer).BuildProduction.ShouldBe(0);
        ruleset.Unit(TestRulesets.Scout).ShouldSatisfyAllConditions(
            static scout => scout.Abilities.ShouldBeEmpty(),
            static scout => scout.Strength.ShouldBe(8),
            static scout => scout.Movement.ShouldBe(4));
        ruleset.Territory.InitialRadius.ShouldBe(1);
        ruleset.Territory.MaxRadius.ShouldBe(3);
        ruleset.Territory.GrowthResource.ShouldBe(TestRulesets.Culture);
        ruleset.Territory.NextTileCost(0).ShouldBe(4);
        ruleset.Territory.NextTileCost(3).ShouldBe(10);
        ruleset.Cities.Yields.ShouldBe([new ResourceAmount(TestRulesets.Science, 1), new ResourceAmount(TestRulesets.Culture, 1)]);
        ruleset.Cities.StartingStock.Select(static amount => amount.Resource).ShouldBe([TestRulesets.Food, TestRulesets.Water, TestRulesets.Wood, TestRulesets.Stone]);
        ruleset.Bridges.Cost.ShouldBe(6);
        ruleset.Resources.Select(static resource => resource.Id.Id).ShouldBe(["food", "water", "wood", "stone", "gold", "science", "culture"]);
        ruleset.Improvements.Select(static improvement => improvement.Id).ShouldBe([TestRulesets.Farm, TestRulesets.LumberCamp, TestRulesets.Mine]);
        ruleset.Improvement(TestRulesets.Mine).Terrains.ShouldBe([TestRulesets.Hills]);
        ruleset.Improvement(TestRulesets.Farm).Yields.ShouldHaveSingleItem().ShouldBe(new ResourceAmount(TestRulesets.Food, 2));
        ruleset.Buildings.Select(static building => building.Id)
            .ShouldBe([TestRulesets.ArmyCamp, TestRulesets.Campus, TestRulesets.TradePost, TestRulesets.CultureCentre, TestRulesets.Harbour, TestRulesets.Workshop, TestRulesets.Barracks, TestRulesets.University, TestRulesets.Market, TestRulesets.Port]);
        ruleset.Building(TestRulesets.ArmyCamp).Production.ShouldBe(1);
        ruleset.Building(TestRulesets.ArmyCamp).ResourceCost.ShouldBe([new ResourceAmount(TestRulesets.Stone, 4), new ResourceAmount(TestRulesets.Wood, 2)]);
        ruleset.Building(TestRulesets.Campus).Upkeep.ShouldBe([new ResourceAmount(TestRulesets.Gold, 1)]);
        ruleset.Building(TestRulesets.TradePost).Upkeep.ShouldBeEmpty();
        ruleset.Unit(TestRulesets.Horseman).ResourceCost.ShouldBe([new ResourceAmount(TestRulesets.Food, 3), new ResourceAmount(TestRulesets.Water, 2)]);
        ruleset.Unit(TestRulesets.Soldier).Upkeep.ShouldBe([new ResourceAmount(TestRulesets.Food, 1), new ResourceAmount(TestRulesets.Water, 1)]);
        ruleset.Upkeep.ShouldBe(new UpkeepRules(SupplyRadius: 1, ShortageDamage: 10, BuildingHealth: 100, BuildingRecovery: 10));
        ruleset.Building(TestRulesets.Harbour).RequiresAdjacentTerrain.ShouldBe([TestRulesets.Lake]);
        ruleset.Unit(TestRulesets.Settler).CanFoundCity.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Settler).CanAttack.ShouldBeFalse();
        ruleset.Unit(TestRulesets.Soldier).CanAttack.ShouldBeTrue();
        ruleset.Unit(TestRulesets.Soldier).ProductionCost.ShouldBe(4);
        ShouldBeTerrain(ruleset.Terrain(TestRulesets.Plains), 1, true, 0, 0, [new(TestRulesets.Food, 2), new(TestRulesets.Water, 1)]);
        ShouldBeTerrain(ruleset.Terrain(TestRulesets.Forest), 2, true, 0, 3, [new(TestRulesets.Food, 1), new(TestRulesets.Water, 1), new(TestRulesets.Wood, 2)]);
        ShouldBeTerrain(ruleset.Terrain(TestRulesets.Hills), 2, true, 1, 3, [new(TestRulesets.Food, 1), new(TestRulesets.Stone, 2)]);
        ShouldBeTerrain(ruleset.Terrain(TestRulesets.Desert), 1, true, 0, 0, []);
        ruleset.Terrain(TestRulesets.Mountains).IsPassable.ShouldBeFalse();
        ruleset.Terrain(TestRulesets.Lake).IsPassable.ShouldBeFalse();
        ruleset.Map.Generation.Patches.Select(static patch => patch.Terrain)
            .ShouldBe([TestRulesets.Forest, TestRulesets.Hills, TestRulesets.Mountains, TestRulesets.Lake, TestRulesets.Desert]);
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
        ruleset.Rivers.Select(static river => river.Id).ShouldBe([TestRulesets.Stream, TestRulesets.River, TestRulesets.DeepRiver]);
        ShouldBeRiver(ruleset.River(TestRulesets.Stream), 0, true, 0);
        ShouldBeRiver(ruleset.River(TestRulesets.River), 1, true, 3);
        ShouldBeRiver(ruleset.River(TestRulesets.DeepRiver), 0, false, 5);
        ruleset.Map.Generation.Rivers.Stages.Select(static stage => stage.River).ShouldBe([TestRulesets.Stream, TestRulesets.River, TestRulesets.DeepRiver]);
        ruleset.Hash.ShouldBe(Ruleset.ComputeHash(TestRulesets.DefaultJson));
    }

    private static void ShouldBeTerrain(TerrainDefinition terrain, int movementCost, bool isPassable, int sightBonus, int defenceBonus, ResourceAmount[] yields)
    {
        (terrain.MovementCost, terrain.IsPassable, terrain.SightBonus, terrain.DefenceBonus).ShouldBe((movementCost, isPassable, sightBonus, defenceBonus));
        terrain.Yields.ShouldBe(yields);
    }

    // Every river gives 1 water to the tiles beside it.
    private static void ShouldBeRiver(RiverDefinition river, int crossingCost, bool isCrossable, int defenceBonus)
    {
        (river.CrossingCost, river.IsCrossable, river.DefenceBonus).ShouldBe((crossingCost, isCrossable, defenceBonus));
        river.Yields.ShouldBe([new ResourceAmount(TestRulesets.Water, 1)]);
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
            "\"city.name.copperhill\": \"Copperhill\"", "\"city.name.copperhill\": \"Copperhill\", \"city.name.copperhill\": \"Copper Hill\"", StringComparison.Ordinal);

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

        ruleset.FindUnit(new UnitType("dragoon")).ShouldBeNull();
        ruleset.FindUnit(default).ShouldBeNull();
        Should.Throw<ArgumentException>(() => ruleset.Unit(new UnitType("dragoon")));
        Should.Throw<ArgumentException>(() => ruleset.Terrain(new TerrainType("tundra")));
        Should.Throw<ArgumentException>(() => ruleset.River(new RiverType("canal")));
        Should.Throw<ArgumentException>(() => ruleset.Text("unit.dragoon.name"));
        ruleset.FindImprovement(new ImprovementType("plantation")).ShouldBeNull();
        ruleset.FindBuilding(new BuildingType("bank")).ShouldBeNull();
        ruleset.FindResource(new ResourceType("iron")).ShouldBeNull();
        Should.Throw<ArgumentException>(() => ruleset.Improvement(new ImprovementType("plantation")));
        Should.Throw<ArgumentException>(() => ruleset.Building(new BuildingType("bank")));
    }

    [Fact]
    public void Names_ComeFromTheTexts()
    {
        var ruleset = TestRulesets.With(json => json["texts"]!["city.name.ambermoor"] = "Amber Moor");

        ruleset.UnitName(TestRulesets.Settler).ShouldBe("Settler");
        ruleset.TerrainName(TestRulesets.Plains).ShouldBe("Plains");
        ruleset.RiverName(TestRulesets.DeepRiver).ShouldBe("Deep river");
        ruleset.ResourceName(TestRulesets.Food).ShouldBe("Food");
        ruleset.ImprovementName(TestRulesets.LumberCamp).ShouldBe("Lumber camp");
        ruleset.BuildingName(TestRulesets.CultureCentre).ShouldBe("Culture centre");
        ruleset.SuggestedCityNames[0].ShouldBe("Amber Moor");
        ruleset.SuggestedCityNames.Length.ShouldBe(30);
    }
}
