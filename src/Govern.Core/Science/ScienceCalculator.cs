using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Science;

/// <summary>
/// What a player's science nodes allow: which units and buildings are unlocked, which unit is the newest of its line, what a
/// unit or building upgrades into, and whether a node can be unlocked. Pure functions of the ruleset and the player's nodes, so
/// the server's rules and the client's panels give the same answers.
/// </summary>
public static class ScienceCalculator
{
    /// <summary>The science the player's enabled nodes take each turn.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <returns>The sum of the enabled nodes' points.</returns>
    public static int Used(Ruleset ruleset, IEnumerable<OwnedNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(nodes);
        return nodes.Where(static node => !node.Disabled).Sum(node => ruleset.FindNode(node.Node)?.Points ?? 0);
    }

    /// <summary>Whether the player owns a node and it is enabled.</summary>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="id">The node's id.</param>
    /// <returns><see langword="true"/> when the node is owned and not disabled.</returns>
    public static bool IsEnabled(IEnumerable<OwnedNode> nodes, string id)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        return nodes.Any(node => node.Node == id && !node.Disabled);
    }

    /// <summary>Whether a unit is unlocked: no node lists it, or the node that does is owned and enabled.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">A unit type of the ruleset.</param>
    /// <returns><see langword="true"/> when the unit is unlocked.</returns>
    public static bool IsUnlocked(Ruleset ruleset, IEnumerable<OwnedNode> nodes, UnitType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return ruleset.NodeUnlocking(type) is not { } node || IsEnabled(nodes, node.Id);
    }

    /// <summary>Whether a building is unlocked: no node lists it, or the node that does is owned and enabled.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">A building of the ruleset.</param>
    /// <returns><see langword="true"/> when the building is unlocked.</returns>
    public static bool IsUnlocked(Ruleset ruleset, IEnumerable<OwnedNode> nodes, BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return ruleset.NodeUnlocking(type) is not { } node || IsEnabled(nodes, node.Id);
    }

    /// <summary>
    /// The newest unlocked version of a unit's line from the unit onwards: the last unit, following
    /// <see cref="Ruleset.ReplacementOf"/>, that is unlocked; the unit itself when none of its replacements is.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">A unit type of the ruleset.</param>
    /// <returns>The newest unlocked version, or <paramref name="type"/>.</returns>
    public static UnitType NewestOfLine(Ruleset ruleset, IEnumerable<OwnedNode> nodes, UnitType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var owned = nodes.ToList();
        var newest = type;
        for (var next = ruleset.ReplacementOf(type); next is not null; next = ruleset.ReplacementOf(next.Id))
        {
            if (IsUnlocked(ruleset, owned, next.Id))
            {
                newest = next.Id;
            }
        }

        return newest;
    }

    /// <summary>Whether a city of the player may start producing a unit: it is unlocked and no newer version of it is.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">A unit type of the ruleset.</param>
    /// <returns><see langword="null"/> when it may; otherwise the violation a <c>SetProductionCommand</c> is rejected with.</returns>
    public static RuleViolation? CheckProduce(Ruleset ruleset, IEnumerable<OwnedNode> nodes, UnitType type)
    {
        var owned = nodes.ToList();
        if (!IsUnlocked(ruleset, owned, type))
        {
            return RuleViolations.Locked;
        }

        return NewestOfLine(ruleset, owned, type) == type ? null : RuleViolations.Replaced;
    }

    /// <summary>
    /// What a city produces after it finished a unit: the newest version of that unit the player may produce, or, when the
    /// player may produce nothing of its line (its node was disabled), the newest version of the ruleset's first unit to produce.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">The unit the city produced.</param>
    /// <returns>The unit the city goes on producing.</returns>
    public static UnitType NextProduction(Ruleset ruleset, IEnumerable<OwnedNode> nodes, UnitType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var owned = nodes.ToList();
        var newest = NewestOfLine(ruleset, owned, type);
        return CheckProduce(ruleset, owned, newest) is null ? newest : NewestOfLine(ruleset, owned, ruleset.Cities.InitialProduction);
    }

    /// <summary>The unit an existing unit can be upgraded into now: the newest unlocked version of its line, if newer than it.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">The unit's type.</param>
    /// <returns>The upgrade's definition, or <see langword="null"/> when there is none.</returns>
    public static UnitDefinition? UnitUpgrade(Ruleset ruleset, IEnumerable<OwnedNode> nodes, UnitType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var newest = NewestOfLine(ruleset, nodes, type);
        return newest == type ? null : ruleset.Unit(newest);
    }

    /// <summary>The building an existing building can be upgraded into now: its upgrade, while that is unlocked.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="type">The building.</param>
    /// <returns>The upgrade's definition, or <see langword="null"/> when there is none or it is locked.</returns>
    public static BuildingDefinition? BuildingUpgrade(Ruleset ruleset, IEnumerable<OwnedNode> nodes, BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return ruleset.UpgradeOf(type) is { } upgrade && IsUnlocked(ruleset, nodes, upgrade.Id) ? upgrade : null;
    }

    /// <summary>Whether the player can unlock a node now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="nodes">The player's nodes.</param>
    /// <param name="income">The player's science per turn.</param>
    /// <param name="id">The node's id.</param>
    /// <returns><see langword="null"/> when they can; otherwise the violation an <c>UnlockNodeCommand</c> is rejected with.</returns>
    public static RuleViolation? CheckUnlock(Ruleset ruleset, ImmutableArray<OwnedNode> nodes, int income, string id)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        if (ruleset.FindNode(id) is not { } node)
        {
            return RuleViolations.UnknownNode;
        }

        if (nodes.Any(owned => owned.Node == id))
        {
            return RuleViolations.NodeOwned;
        }

        if (!node.Requires.All(required => IsEnabled(nodes, required)))
        {
            return RuleViolations.NodeNeedsPrerequisite;
        }

        return node.Points > income - Used(ruleset, nodes) ? RuleViolations.NotEnoughScience : null;
    }

    /// <summary>The science a set of yields holds: the amount of the ruleset's science resource.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="yields">A city's income.</param>
    /// <returns>The science in it.</returns>
    public static int ScienceIn(Ruleset ruleset, IEnumerable<ResourceAmount> yields)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return YieldCalculator.AmountOf(yields, ruleset.Science.Resource);
    }
}
