namespace Govern.Core.Rulesets;

/// <summary>
/// Bridges (the ruleset's <c>bridges</c> section): a unit with <see cref="Units.UnitAbility.BuildBridge"/> builds one on a river
/// edge next to it, and units then cross that edge as if there were no river.
/// </summary>
/// <param name="Cost">Production points needed to build a bridge.</param>
public sealed record BridgeRules(int Cost);
