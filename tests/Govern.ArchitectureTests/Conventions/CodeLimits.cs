namespace Govern.ArchitectureTests.Conventions;

/// <summary>
/// Code size limits for hand-written production code (see "Size limits" in <c>.claude/rules/csharp-style.md</c>).
/// </summary>
public static class CodeLimits
{
    /// <summary>
    /// Maximum physical lines per <c>.cs</c> file.
    /// </summary>
    public const int MaxFileLines = 300;

    /// <summary>
    /// Maximum lines of a member body (methods, constructors, local functions, accessors, operators),
    /// measured from the opening brace or <c>=&gt;</c> to the end of the body.
    /// </summary>
    public const int MaxMemberBodyLines = 40;

    /// <summary>
    /// Maximum parameters of a method, local function or delegate (a <c>CancellationToken</c> is not counted).
    /// </summary>
    public const int MaxMethodParameters = 5;

    /// <summary>
    /// Maximum parameters of a constructor, including class primary constructors (positional records are exempt).
    /// </summary>
    public const int MaxConstructorParameters = 6;

    /// <summary>
    /// Path fragments and file-name suffixes of generated code that is never measured.
    /// </summary>
    public static readonly string[] GeneratedPathMarkers =
        ["/Persistence/Migrations/", ".Designer.cs", ".g.cs", ".generated.cs"];
}
