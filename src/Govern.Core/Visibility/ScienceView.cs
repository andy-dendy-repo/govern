using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Science;

namespace Govern.Core.Visibility;

/// <summary>The viewer's own science (G11a); an opponent's science is never in the view.</summary>
/// <param name="Income">The viewer's science per turn: what their cities make now.</param>
/// <param name="Nodes">The viewer's science nodes, in the order they were unlocked; <see langword="null"/> while there are none.</param>
public sealed record ScienceView(
    int Income,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<OwnedNode>? Nodes = null)
{
    /// <summary>The viewer's science nodes (empty when none).</summary>
    [JsonIgnore]
    public ImmutableArray<OwnedNode> NodeList => Nodes ?? [];
}
