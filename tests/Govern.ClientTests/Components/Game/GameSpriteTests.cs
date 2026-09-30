using System.Reflection;
using Govern.Client.Components.Game;
using Govern.Client.Helpers;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class GameSpriteTests : ClientTestContext
{
    [Fact]
    public void Render_EveryRulesetUnit_HasASymbol()
    {
        var cut = Render<GameSprite>();

        var missing = TestViews.Ruleset.Units.Select(static unit => Icons.Unit(unit.Id))
            .Where(id => cut.FindAll($"symbol#{id}").Count != 1)
            .ToList();

        missing.ShouldBeEmpty("every unit in the ruleset needs its symbol in Components/Game/GameSprite.razor");
    }

    [Fact]
    public void Render_EveryRulesetTerrain_HasASymbol()
    {
        var cut = Render<GameSprite>();

        var missing = TestViews.Ruleset.Terrains.Select(static terrain => Icons.Terrain(terrain.Id))
            .Where(id => cut.FindAll($"symbol#{id}").Count != 1)
            .ToList();

        missing.ShouldBeEmpty("every terrain in the ruleset needs its symbol in Components/Game/GameSprite.razor");
    }

    [Fact]
    public void Render_EveryNamedIcon_HasASymbol()
    {
        var cut = Render<GameSprite>();

        var missing = typeof(Icons).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral)
            .Select(static field => (string)field.GetRawConstantValue()!)
            .Where(id => cut.FindAll($"symbol#{id}").Count != 1)
            .ToList();

        missing.ShouldBeEmpty("every Icons constant needs its symbol in Components/Game/GameSprite.razor");
    }

    [Fact]
    public void Render_Sprite_IsHiddenFromScreenReaders()
    {
        var cut = Render<GameSprite>();

        cut.Find("svg.game-sprite").GetAttribute("aria-hidden").ShouldBe("true");
    }
}
