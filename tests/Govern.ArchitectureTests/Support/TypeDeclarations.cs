using System.Text.RegularExpressions;

namespace Govern.ArchitectureTests.Support;

/// <summary>
/// Lightweight source scan for top-level type declarations (file-scoped namespaces keep them at column 0).
/// </summary>
public static partial class TypeDeclarations
{
    /// <summary>
    /// Names of top-level types in <paramref name="source"/> that are not <c>file</c>-scoped.
    /// </summary>
    /// <param name="source">C# source text.</param>
    /// <returns>Declared type names in source order.</returns>
    public static IReadOnlyList<string> NonFileScopedTopLevelTypes(string source) =>
        TopLevelTypeRegex().Matches(source)
            .Where(static match => !match.Groups["modifiers"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("file"))
            .Select(static match => match.Groups["name"].Value)
            .ToList();

    [GeneratedRegex(
        @"^(?<modifiers>(?:(?:public|internal|file|sealed|static|abstract|partial|readonly|ref)\s+)*)(?:record\s+(?:class\s+|struct\s+)?|class\s+|struct\s+|interface\s+|enum\s+)(?<name>\w+)",
        RegexOptions.Multiline)]
    private static partial Regex TopLevelTypeRegex();
}
