namespace Govern.ArchitectureTests.Conventions;

/// <summary>
/// Allowed project references per project (see <c>.claude/rules/project-map.md</c>).
/// </summary>
public static class Layers
{
    /// <summary>
    /// Projects that are deliberately excluded from all architecture rules.
    /// </summary>
    public static readonly string[] Ignored = ["Govern.Core"];

    /// <summary>
    /// Source projects <c>Govern.UnitTests</c> may reference: only pure, framework-agnostic code (see <c>.claude/rules/unit-tests.md</c>).
    /// </summary>
    public static readonly string[] UnitTestReferences = ["Govern.Domain", "Govern.Utils"];

    /// <summary>
    /// Every known source project mapped to the projects it may reference.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>
    {
        ["Govern.Utils"] = [],
        ["Govern.Domain"] = ["Govern.Utils"],
        ["Govern.Application.Common"] = ["Govern.Domain", "Govern.Utils"],
        ["Govern.Application"] = ["Govern.Application.Common", "Govern.Domain", "Govern.Utils"],
        ["Govern.Infrastructure"] = ["Govern.Application", "Govern.Application.Common", "Govern.Domain", "Govern.Utils"],
        ["Govern.Api"] = ["Govern.Application", "Govern.Application.Common", "Govern.Infrastructure", "Govern.Domain", "Govern.Utils"],
        ["Govern.Client"] = [],
    };

    /// <summary>
    /// Regex matching every namespace of the given project, excluding namespaces of projects that share its prefix.
    /// </summary>
    /// <param name="project">Project (and root namespace) name, e.g. <c>Govern.Application</c>.</param>
    /// <returns>A regular expression for <c>ResideInNamespaceMatching</c>.</returns>
    public static string NamespacePattern(string project)
    {
        var nestedProjects = AllowedReferences.Keys
            .Where(other => other.StartsWith(project + ".", StringComparison.Ordinal))
            .Select(other => System.Text.RegularExpressions.Regex.Escape(other[(project.Length + 1)..]))
            .ToArray();

        var exclusion = nestedProjects.Length == 0 ? string.Empty : $"(?!(?:{string.Join('|', nestedProjects)})(?:\\.|$))";
        return $"^{System.Text.RegularExpressions.Regex.Escape(project)}(?:\\.{exclusion}.*)?$";
    }
}
