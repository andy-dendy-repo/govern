namespace Govern.Core.Rulesets;

/// <summary>
/// One reason a ruleset document was rejected.
/// </summary>
/// <param name="Path">Where in the document, as a JSON path (<c>$.units[1].sight</c>).</param>
/// <param name="Message">What is wrong.</param>
public sealed record RulesetProblem(string Path, string Message)
{
    /// <summary>The problem as one line.</summary>
    /// <returns><c>path: message</c>.</returns>
    public override string ToString() => $"{Path}: {Message}";
}
