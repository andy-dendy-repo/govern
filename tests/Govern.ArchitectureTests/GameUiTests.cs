using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Govern.ArchitectureTests.Support;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;

namespace Govern.ArchitectureTests;

/// <summary>
/// The game UI design system (<c>docs/features/game-ui.md</c>, <c>.claude/rules/game-ui.md</c>): colours are written only as
/// tokens in <c>wwwroot/css/tokens.css</c>, and every ruleset terrain has its palette and every river kind its style, so a new
/// terrain or river can't ship unstyled.
/// Icons per ruleset id are checked by <c>Govern.ClientTests</c> (<c>GameSpriteTests</c>), which can render the sprite.
/// </summary>
public partial class GameUiTests
{
    private static readonly string ClientRoot = Path.Combine(SourceTree.SourceRoot, "Govern.Client");
    private static readonly string CssRoot = Path.Combine(ClientRoot, "wwwroot", "css");
    private static readonly string TokensFile = Path.Combine(CssRoot, "tokens.css");
    private static readonly string Board3DRoot = Path.Combine(ClientRoot, "wwwroot", "js", "board3d");
    private static readonly string UnitModelsRoot = Path.Combine(ClientRoot, "wwwroot", "models", "units");

    [Fact]
    public void Css_UsesColourValuesOnlyInTokens()
    {
        var violations = Directory.EnumerateFiles(CssRoot, "*.css", SearchOption.AllDirectories)
            .Where(static file => file != TokensFile)
            .SelectMany(static file => File.ReadLines(file)
                .Select((line, index) => (line, index))
                .Where(static entry => !entry.line.Contains("data:image", StringComparison.Ordinal)
                    && ColourValueRegex().IsMatch(entry.line))
                .Select(entry => $"{SourceTree.Relative(file)}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        violations.ShouldBeEmpty("colours are design tokens: define them in wwwroot/css/tokens.css and use var(--gv-…)");
    }

    [Fact]
    public void Board3DScripts_TakeColoursOnlyFromTokens()
    {
        var violations = Directory.EnumerateFiles(Board3DRoot, "*.js", SearchOption.AllDirectories)
            .SelectMany(static file => File.ReadLines(file)
                .Select((line, index) => (line, index))
                .Where(static entry => ScriptColourValueRegex().IsMatch(entry.line))
                .Select(entry => $"{SourceTree.Relative(file)}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        violations.ShouldBeEmpty("the 3D board reads its colours from the --gv-* tokens (js/board3d/palette.js), never literals");
    }

    [Fact]
    public void Board3DModels_HaveABuilderPerFeatureModel()
    {
        var builders = ModelBuilderRegex().Matches(File.ReadAllText(Path.Combine(Board3DRoot, "models.js")))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Enum.GetNames<FeatureModel>().Select(static name => name.ToLowerInvariant()).Where(name => !builders.Contains(name)).ToList();

        missing.ShouldBeEmpty("every FeatureModel needs its builder in js/board3d/models.js, or it is drawn as a stand-in");
    }

    [Fact]
    public void Board3DPieces_HaveABuilderPerPieceModel()
    {
        var builders = PieceBuilderRegex().Matches(File.ReadAllText(Path.Combine(Board3DRoot, "pieces.js")))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Enum.GetNames<PieceModel>().Select(static name => name.ToLowerInvariant()).Where(name => !builders.Contains(name)).ToList();

        missing.ShouldBeEmpty("every PieceModel needs its builder in js/board3d/pieces.js, or it is drawn as a soldier");
    }

    [Fact]
    public void Board3DPieces_EveryFigureHasItsBakedModel()
    {
        var pieces = File.ReadAllText(Path.Combine(Board3DRoot, "pieces.js"));
        var listed = FigureListRegex().Match(pieces);
        listed.Success.ShouldBeTrue("js/board3d/pieces.js lists the baked characters in FIGURES");
        var figures = FigureNameRegex().Matches(listed.Groups[1].Value).Select(static match => match.Groups[1].Value).ToList();
        var used = FigureUseRegex().Matches(pieces).Select(static match => match.Groups[1].Value).Distinct(StringComparer.Ordinal);

        used.Except(figures, StringComparer.Ordinal).ShouldBeEmpty("a piece may only stand on a character listed in FIGURES");
        var baked = BakedUnitRegex().Matches(File.ReadAllText(Path.Combine(SourceTree.RepositoryRoot, "tools", "unit-models", "bake.mjs")))
            .Select(static match => match.Groups[1].Value);
        baked.ShouldBe(figures, ignoreOrder: true, "tools/unit-models/bake.mjs bakes exactly the characters pieces.js loads");
        var shapes = ShapeNameRegex().Matches(pieces).Select(static match => match.Groups[1].Value);
        figures.Intersect(shapes, StringComparer.Ordinal).ShouldBeEmpty("a figure named like a shape would be drawn as that shape");
        foreach (var figure in figures)
        {
            var file = Path.Combine(UnitModelsRoot, $"{figure}.json");
            File.Exists(file).ShouldBeTrue($"wwwroot/models/units/{figure}.json is baked by tools/unit-models (npm run bake)");
            using var model = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var part in new[] { "body", "seat" })
            {
                model.RootElement.GetProperty(part).GetProperty("indices").GetArrayLength().ShouldBeGreaterThan(0, $"{figure}.json has its {part} part");
            }
        }
    }

    [Fact]
    public void Board3DTerrain_SharesTheWaterLevelWithTerrainReliefs()
    {
        var match = WaterLevelRegex().Match(File.ReadAllText(Path.Combine(Board3DRoot, "terrain.js")));

        match.Success.ShouldBeTrue("js/board3d/terrain.js declares WATER_LEVEL");
        double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            .ShouldBe(TerrainReliefs.WaterLevel, "things on a lake stand on the water the renderer draws");
    }

    [Fact]
    public void Razor_WritesNoColourValues()
    {
        var violations = SourceTree.RazorFiles(ClientRoot)
            .SelectMany(static file => File.ReadLines(file)
                .Select((line, index) => (line, index))
                .Where(static entry => RazorColourAttributeRegex().IsMatch(entry.line))
                .Select(entry => $"{SourceTree.Relative(file)}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        violations.ShouldBeEmpty("components take colours from CSS classes backed by tokens (seat-1, terrain-plains), never inline values");
    }

    [Fact]
    public void Tokens_DefineAPaletteForEveryTerrain()
    {
        var tokens = File.ReadAllText(TokensFile);

        var missing = RulesetTerrainIds()
            .SelectMany(static id => new[] { $"--gv-terrain-{id}:", $"--gv-terrain-{id}-explored:" })
            .Where(token => !tokens.Contains(token, StringComparison.Ordinal))
            .ToList();

        missing.ShouldBeEmpty("every ruleset terrain needs a visible and an explored colour in wwwroot/css/tokens.css");
    }

    [Fact]
    public void BoardCss_MapsEveryTerrainToItsPalette()
    {
        var board = File.ReadAllText(Path.Combine(CssRoot, "board.css"));

        var missing = RulesetTerrainIds()
            .Where(id => !board.Contains($".terrain-{id} {{", StringComparison.Ordinal)
                || !board.Contains($"--gv-terrain: var(--gv-terrain-{id});", StringComparison.Ordinal))
            .ToList();

        missing.ShouldBeEmpty("every ruleset terrain needs its .terrain-<id> rule in wwwroot/css/board.css");
    }

    [Fact]
    public void BoardCss_StylesEveryRiver()
    {
        var board = File.ReadAllText(Path.Combine(CssRoot, "board.css"));

        var missing = RulesetIds("rivers").Where(id => !board.Contains($".river-{id} {{", StringComparison.Ordinal)).ToList();

        missing.ShouldBeEmpty("every ruleset river needs its .river-<id> rule (its width) in wwwroot/css/board.css");
    }

    private static List<string> RulesetTerrainIds() => RulesetIds("terrains");

    // The ids of one section (terrains, rivers) across every ruleset file; older versions may lack a section.
    private static List<string> RulesetIds(string section)
    {
        var rulesets = Path.Combine(SourceTree.RepositoryRoot, "rulesets");
        return Directory.EnumerateFiles(rulesets, "*.json", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                return document.RootElement.TryGetProperty(section, out var entries)
                    ? entries.EnumerateArray().Select(static entry => entry.GetProperty("id").GetString()!).ToList()
                    : [];
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // A hex colour, a functional colour (rgb, rgba, hsl, hsla, hwb, lab, lch, oklab, oklch, color), or a common named colour
    // as a property value.
    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(|:\s*[^;{]*\b(white|black|red|green|blue|yellow|orange|purple|gray|grey|silver|lightyellow|navy|teal|maroon)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ColourValueRegex();

    // A colour literal in a script: a CSS hex or functional colour in a string, a 0xRRGGBB number, or a named colour passed to
    // Three.js (new Color('white'), setStyle('red')).
    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b0x[0-9a-fA-F]{6}\b|\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(|\b(Color|setStyle|setColorName)\(\s*['""`]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptColourValueRegex();

    // A model builder in models.js: `    name: c => [`.
    [GeneratedRegex(@"^ {4}([a-z]+): c => \[", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ModelBuilderRegex();

    // A piece builder in pieces.js: `    name: c => [`.
    [GeneratedRegex(@"^ {4}([a-z]+): c => \[", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PieceBuilderRegex();

    // The baked characters pieces.js loads: `const FIGURES = ['soldier', …];`.
    [GeneratedRegex(@"const FIGURES = \[([^\]]*)\];", RegexOptions.CultureInvariant)]
    private static partial Regex FigureListRegex();

    [GeneratedRegex(@"'([a-z]+)'", RegexOptions.CultureInvariant)]
    private static partial Regex FigureNameRegex();

    // A unit in tools/unit-models/bake.mjs: `    name: { character: …`.
    [GeneratedRegex(@"^ {4}([a-z]+): \{ character:", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BakedUnitRegex();

    // A shared shape in pieces.js: `    name: new XxxGeometry(`.
    [GeneratedRegex(@"^ {4}([a-z]+): new [A-Za-z]+Geometry\(", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ShapeNameRegex();

    // A piece standing on a baked character: `standing(c, 'name')` or `character(c, 'name', …)`.
    [GeneratedRegex(@"\b(?:standing|character)\(c, '([a-z]+)'", RegexOptions.CultureInvariant)]
    private static partial Regex FigureUseRegex();

    [GeneratedRegex(@"const WATER_LEVEL = ([0-9.]+);", RegexOptions.CultureInvariant)]
    private static partial Regex WaterLevelRegex();

    // fill, stroke, color, background or style attributes carrying a colour value (hex, functional or a common name).
    [GeneratedRegex(@"\b(fill|stroke|color|background|style)\s*=\s*""[^""]*(#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(|\b(white|black|red|green|blue|yellow|orange|purple|gray|grey)\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RazorColourAttributeRegex();
}
