using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// The outcome of <see cref="PredefinedMap.Parse"/>: a valid map, or every problem found.
/// </summary>
/// <param name="Map">The map; <see langword="null"/> when there are problems.</param>
/// <param name="Problems">Every problem found, with its JSON path; empty when the map is valid.</param>
public sealed record PredefinedMapParseResult(PredefinedMap? Map, ImmutableArray<RulesetProblem> Problems)
{
    /// <summary>Whether the document is a valid map.</summary>
    [MemberNotNullWhen(true, nameof(Map))]
    public bool IsValid => Map is not null;
}
