using Govern.Core.Cities;
using Govern.Core.Maps;
using Govern.Core.Units;

namespace Govern.UnitTests.Core.Units;

// Fog-of-war invariants: pathfinding and city spacing read the full state, so rejections only stay leak-free while
// nothing a unit can reach or be blocked by lies beyond what it sees. Changing a stat that breaks these needs a rule change.
public class UnitCatalogTests
{
    public static TheoryData<UnitType> AllUnitTypes => new(Enum.GetValues<UnitType>());

    [Theory]
    [MemberData(nameof(AllUnitTypes))]
    public void Get_EveryUnit_SeesAtLeastAsFarAsItCanMove(UnitType type)
    {
        var definition = UnitCatalog.Get(type);
        var cheapestStep = Enum.GetValues<TerrainType>().Min(terrain => TerrainCatalog.Get(terrain).MovementCost);

        definition.Type.ShouldBe(type);
        (definition.Movement / cheapestStep).ShouldBeLessThanOrEqualTo(definition.Sight);
    }

    [Fact]
    public void Get_Settler_SeesEveryCityThatBlocksFounding()
    {
        (City.MinimumSpacing - 1).ShouldBeLessThanOrEqualTo(UnitCatalog.Get(UnitType.Settler).Sight);
    }

    [Fact]
    public void Get_UnknownType_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => UnitCatalog.Get((UnitType)99));
    }
}
