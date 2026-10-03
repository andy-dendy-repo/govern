namespace Govern.Core.Economy;

/// <summary>
/// A city's demand level for a tradable resource (G17), in percent: it rises while the city wants the resource and gets no deal
/// for it (or can't pay its upkeep in it), and falls while the city offers it and nobody takes it. A resource's value in the
/// city is its <see cref="ResourceDefinition.TradeValue"/> times this level; levels left out are
/// <see cref="Rulesets.TradeRules.BaseDemandPercent"/>.
/// </summary>
/// <param name="Resource">A tradable resource.</param>
/// <param name="Percent">The level, between <see cref="Rulesets.TradeRules.MinDemandPercent"/> and <see cref="Rulesets.TradeRules.MaxDemandPercent"/>.</param>
public sealed record ResourceDemand(ResourceType Resource, int Percent);
