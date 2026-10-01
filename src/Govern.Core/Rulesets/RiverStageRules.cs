using Govern.Core.Map;

namespace Govern.Core.Rulesets;

/// <summary>
/// One stretch of a generated river (an entry of <c>map.generation.rivers.stages</c>).
/// </summary>
/// <param name="River">The river kind of this stretch.</param>
/// <param name="FromSegment">The edge number, counted from the source (0), where this stretch starts.</param>
public sealed record RiverStageRules(RiverType River, int FromSegment);
