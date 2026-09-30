using System.Text.Json;
using System.Text.RegularExpressions;
using Govern.ArchitectureTests.Support;

namespace Govern.ArchitectureTests;

/// <summary>
/// The game UI design system (<c>docs/features/game-ui.md</c>, <c>.claude/rules/game-ui.md</c>): colours are written only as
/// tokens in <c>wwwroot/css/tokens.css</c>, and every ruleset terrain has its palette, so a new terrain can't ship uncoloured.
/// Icons per ruleset id are checked by <c>Govern.ClientTests</c> (<c>GameSpriteTests</c>), which can render the sprite.
/// </summary>
public partial class GameUiTests
{
    private static readonly string ClientRoot = Path.Combine(SourceTree.SourceRoot, "Govern.Client");
    private static readonly string CssRoot = Path.Combine(ClientRoot, "wwwroot", "css");
    private static readonly string TokensFile = Path.Combine(CssRoot, "tokens.css");

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

    private static List<string> RulesetTerrainIds()
    {
        var rulesets = Path.Combine(SourceTree.RepositoryRoot, "rulesets");
        return Directory.EnumerateFiles(rulesets, "*.json", SearchOption.AllDirectories)
            .SelectMany(static file =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                return document.RootElement.GetProperty("terrains").EnumerateArray()
                    .Select(static terrain => terrain.GetProperty("id").GetString()!)
                    .ToList();
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // A hex colour, or a functional colour (rgb, rgba, hsl, hsla, hwb, lab, lch, oklab, oklch, color).
    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(", RegexOptions.CultureInvariant)]
    private static partial Regex ColourValueRegex();

    // fill, stroke, color, background or style attributes carrying a colour value.
    [GeneratedRegex(@"\b(fill|stroke|color|background|style)\s*=\s*""[^""]*(#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\()", RegexOptions.CultureInvariant)]
    private static partial Regex RazorColourAttributeRegex();
}
