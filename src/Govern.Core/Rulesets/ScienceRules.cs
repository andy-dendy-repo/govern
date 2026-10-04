using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Science;

namespace Govern.Core.Rulesets;

/// <summary>
/// The science tree (the ruleset's <c>science</c> section, G11a). Science is a budget, not a stock: each turn a player has the
/// <see cref="Resource"/> their cities make, and every node they own takes its <see cref="ScienceNode.Points"/> of it. A unit
/// or building listed by a node is locked until the node is owned and enabled; one listed by none is always available.
/// </summary>
/// <param name="Resource">
/// The resource that counts as science: cities make it like any yield, but it goes to the player's science per turn instead of
/// the city's stock.
/// </param>
/// <param name="Nodes">Every node, in the order the tree lists them; a node's prerequisites come before it.</param>
public sealed record ScienceRules(ResourceType Resource, ImmutableArray<ScienceNode> Nodes);
