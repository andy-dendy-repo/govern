using System.Reflection;
using Govern.Application.Games.Models;
using Govern.Core.Engine;
using Govern.Utils.Results;

namespace Govern.IntegrationTests.Features.Games;

/// <summary>
/// Engine rule violations reach clients with their own codes next to the Games errors (both use the <c>game.</c> prefix),
/// so the two sets must never share a code.
/// </summary>
public class GameErrorCodesTests
{
    [Fact]
    public void RuleViolationCodes_AndGameErrorCodes_AreDisjoint()
    {
        var violationCodes = typeof(RuleViolations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => ((RuleViolation)field.GetValue(null)!).Code)
            .ToList();
        var errorCodes = typeof(GameErrors).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => ((Error)field.GetValue(null)!).Code)
            .ToList();

        violationCodes.ShouldNotBeEmpty();
        errorCodes.ShouldNotBeEmpty();
        violationCodes.Intersect(errorCodes).ShouldBeEmpty();
    }
}
