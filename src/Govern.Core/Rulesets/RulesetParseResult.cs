using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Govern.Core.Rulesets;

/// <summary>
/// The outcome of <see cref="Ruleset.Parse"/>: a valid ruleset, or every problem found.
/// </summary>
/// <param name="Ruleset">The ruleset; <see langword="null"/> when there are problems.</param>
/// <param name="Problems">Every problem found; empty when the ruleset is valid.</param>
public sealed record RulesetParseResult(Ruleset? Ruleset, ImmutableArray<RulesetProblem> Problems)
{
    /// <summary>Whether the document is a valid ruleset.</summary>
    [MemberNotNullWhen(true, nameof(Ruleset))]
    public bool IsValid => Ruleset is not null;
}
