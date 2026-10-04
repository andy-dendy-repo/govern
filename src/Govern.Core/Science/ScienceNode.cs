using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Units;

namespace Govern.Core.Science;

/// <summary>
/// One item of the science tree (the ruleset's <c>science.nodes</c>). A player owns it from the moment they unlock it, and it
/// takes <see cref="Points"/> of their science every turn for as long as they own it; it never accumulates.
/// </summary>
/// <param name="Id">The node's id, a lower snake_case ruleset id; its name is the text <c>science.{id}.name</c>.</param>
/// <param name="Points">Science per turn the node takes while it is owned and enabled.</param>
/// <param name="Requires">Nodes the player must own, enabled, to unlock it; each is listed before it in the ruleset.</param>
/// <param name="Units">Units that can be produced (or upgraded into) only while the node is owned and enabled.</param>
/// <param name="Buildings">Buildings that can be produced (or upgraded into) only while the node is owned and enabled.</param>
public sealed record ScienceNode(
    string Id,
    int Points,
    ImmutableArray<string> Requires,
    ImmutableArray<UnitType> Units,
    ImmutableArray<BuildingType> Buildings);
