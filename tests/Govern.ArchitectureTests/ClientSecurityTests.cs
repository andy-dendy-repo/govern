using System.Text.Json;
using System.Text.RegularExpressions;
using Govern.ArchitectureTests.Support;

namespace Govern.ArchitectureTests;

/// <summary>
/// The client stores tokens in localStorage (ADR 0002), so XSS prevention is a security requirement:
/// a strict CSP on the Static Web App, self-hosted assets only, and no raw HTML rendering.
/// </summary>
public partial class ClientSecurityTests
{
    private static readonly string ClientRoot = Path.Combine(SourceTree.SourceRoot, "Govern.Client");
    private static readonly string WebRoot = Path.Combine(ClientRoot, "wwwroot");

    // The only file allowed to read or write localStorage, where the access and refresh tokens live (ADR 0002).
    private static readonly string TokenStoreFile = Path.Combine(ClientRoot, "Auth", "TokenStore.cs");

    private static readonly string[] RawHtmlOrEvalTokens =
        ["MarkupString", "AddMarkupContent", "RadzenHtmlEditor", "RadzenMarkdown", "\"eval\"", "eval(", "\"Function\""];

    public static TheoryData<string> RequiredCspDirectives => new(
        "default-src 'self'",
        "script-src 'self' 'wasm-unsafe-eval' __IMPORTMAP_HASH__",
        "style-src 'self'",
        "style-src-attr 'unsafe-inline'",
        "img-src 'self' data:",
        "font-src 'self'",
        "connect-src 'self' __API_ORIGIN__",
        "frame-ancestors 'none'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "upgrade-insecure-requests");

    [Theory]
    [MemberData(nameof(RequiredCspDirectives))]
    public void StaticWebAppCsp_ContainsRequiredDirective(string directive)
    {
        CspDirectives().ShouldContain(directive, $"staticwebapp.config.json CSP must contain \"{directive}\"");
    }

    [Fact]
    public void StaticWebAppCsp_ScriptSourcesAllowNoInlineEvalOrWildcards()
    {
        // script-src-elem / script-src-attr / worker-src override script-src, and default-src is their fallback.
        string[] forbidden = ["'unsafe-inline'", "'unsafe-eval'", "'unsafe-hashes'", "'strict-dynamic'", "*", "data:", "blob:", "http:", "https:"];

        var violations = CspDirectives()
            .Where(static directive => ScriptDirectiveRegex().IsMatch(directive))
            .SelectMany(directive => forbidden
                .Where(source => directive.Split(' ').Contains(source, StringComparer.Ordinal))
                .Select(source => $"{directive.Split(' ')[0]} allows {source}"))
            .ToList();

        violations.ShouldBeEmpty("scripts load from the app's own origin only; inline scripts are hashed, never 'unsafe-inline'");
    }

    [Fact]
    public void StaticWebAppRoutes_DoNotOverrideHeadersAndBlockBuiltInAuth()
    {
        var routes = StaticWebAppConfig().TryGetProperty("routes", out var value) ? value.EnumerateArray().ToList() : [];

        routes.Where(static route => route.TryGetProperty("headers", out _))
            .Select(static route => route.GetProperty("route").GetString())
            .ShouldBeEmpty("a route-level header replaces the global CSP for that route");
        routes.ShouldContain(
            static route => route.GetProperty("route").GetString() == "/.auth/*" && route.GetProperty("statusCode").GetInt32() == 404,
            "the Static Web App's built-in auth providers are not used and stay disabled");
    }

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Strict-Transport-Security", "max-age=31536000; includeSubDomains")]
    [InlineData("Cross-Origin-Opener-Policy", "same-origin")]
    [InlineData("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()")]
    public void StaticWebApp_SendsSecurityHeader(string header, string value)
    {
        GlobalHeaders().ShouldContainKeyAndValue(header, value);
    }

    [Fact]
    public void IndexHtml_LoadsOnlySelfHostedAssetsWithoutInlineScripts()
    {
        var html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));
        var violations = new List<string>();

        violations.AddRange(ExternalAssetRegex().Matches(html).Select(static match => $"external asset: {match.Value}"));
        violations.AddRange(InlineHandlerRegex().Matches(html).Select(static match => $"inline event handler: {match.Value.Trim()}"));
        violations.AddRange(InlineScriptRegex().Matches(html)
            .Where(static match => !(match.Groups["attributes"].Value.Contains("type=\"importmap\"", StringComparison.Ordinal)
                && match.Groups["body"].Value.Length == 0))
            .Select(static match => $"inline script: {match.Value}"));

        violations.ShouldBeEmpty("index.html may contain only the empty import map (filled at publish and hashed into the CSP by .github/scripts/configure-client.sh)");
    }

    [Fact]
    public void ClientSources_DoNotRenderRawHtmlOrEvalJavaScript()
    {
        var violations = SourceTree.CSharpFiles(ClientRoot).Concat(SourceTree.RazorFiles(ClientRoot))
            .SelectMany(static file => File.ReadLines(file)
                .Select((line, index) => (line, index))
                .Where(static entry => RawHtmlOrEvalTokens.Any(token => entry.line.Contains(token, StringComparison.Ordinal)))
                .Select(entry => $"{SourceTree.Relative(file)}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        violations.ShouldBeEmpty("raw HTML and eval bypass Blazor's HTML encoding; render text with Radzen components instead");
    }

    [Fact]
    public void ClientSources_TouchLocalStorageOnlyInTokenStore()
    {
        var files = SourceTree.CSharpFiles(ClientRoot)
            .Concat(SourceTree.RazorFiles(ClientRoot))
            .Concat(Directory.EnumerateFiles(WebRoot, "*.js", SearchOption.AllDirectories))
            .Append(Path.Combine(WebRoot, "index.html"))
            .Where(static file => !string.Equals(Path.GetFullPath(file), Path.GetFullPath(TokenStoreFile), StringComparison.Ordinal));

        var violations = files
            .SelectMany(static file => File.ReadLines(file)
                .Select((line, index) => (line, index))
                .Where(static entry => entry.line.Contains("localStorage", StringComparison.OrdinalIgnoreCase))
                .Select(entry => $"{SourceTree.Relative(file)}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        violations.ShouldBeEmpty("tokens live in localStorage; only src/Govern.Client/Auth/TokenStore.cs may touch it (inject TokenStore instead)");
    }

    [Fact]
    public void ClientAppSettings_HoldNoSecrets()
    {
        var violations = Directory.EnumerateFiles(WebRoot, "appsettings*.json")
            .SelectMany(static file => PropertyNames(JsonDocument.Parse(File.ReadAllText(file)).RootElement)
                .Where(static name => SecretKeyRegex().IsMatch(name))
                .Select(name => $"{SourceTree.Relative(file)}: '{name}'"))
            .ToList();

        violations.ShouldBeEmpty("wwwroot/appsettings*.json is downloaded by every visitor: public URLs and client ids only");
    }

    private static JsonElement StaticWebAppConfig() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(WebRoot, "staticwebapp.config.json"))).RootElement;

    private static Dictionary<string, string> GlobalHeaders() =>
        StaticWebAppConfig().GetProperty("globalHeaders").EnumerateObject()
            .ToDictionary(static header => header.Name, static header => header.Value.GetString() ?? string.Empty, StringComparer.Ordinal);

    private static List<string> CspDirectives() =>
        GlobalHeaders()["Content-Security-Policy"]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(static p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    [GeneratedRegex(@"^(?:script-src(?:-elem|-attr)?|worker-src|default-src)\s")]
    private static partial Regex ScriptDirectiveRegex();

    [GeneratedRegex(@"<(?:script|link)\b[^>]*\b(?:src|href)\s*=\s*[""']?(?:https?:)?//[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalAssetRegex();

    [GeneratedRegex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandlerRegex();

    [GeneratedRegex(@"<script(?![^>]*\bsrc=)(?<attributes>[^>]*)>(?<body>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineScriptRegex();

    [GeneratedRegex("secret|password|key|token|connectionstring", RegexOptions.IgnoreCase)]
    private static partial Regex SecretKeyRegex();
}
