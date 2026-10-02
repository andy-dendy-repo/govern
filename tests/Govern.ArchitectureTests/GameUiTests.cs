using System.Text.Json;
using System.Text.RegularExpressions;
using Govern.ArchitectureTests.Support;

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

    // fill, stroke, color, background or style attributes carrying a colour value (hex, functional or a common name).
    [GeneratedRegex(@"\b(fill|stroke|color|background|style)\s*=\s*""[^""]*(#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(|\b(white|black|red|green|blue|yellow|orange|purple|gray|grey)\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RazorColourAttributeRegex();
}
