using Govern.Application.Games.Services;
using Govern.Core.Rulesets;

namespace Govern.IntegrationTests.Support;

/// <summary>The ruleset the Api plays every game with, for tests that build or inspect game states directly.</summary>
internal static class TestRulesets
{
    public static readonly Ruleset Default = new DefaultRulesetService().Ruleset;
}
