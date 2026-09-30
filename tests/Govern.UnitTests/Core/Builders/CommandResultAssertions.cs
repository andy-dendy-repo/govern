using Govern.Core.Engine;

namespace Govern.UnitTests.Core.Builders;

internal static class CommandResultAssertions
{
    public static GameUpdate ShouldSucceed(this CommandResult result)
    {
        result.IsSuccess.ShouldBeTrue($"expected success but got {result.Violation?.Code}");
        return result.Update!;
    }

    public static void ShouldFailWith(this CommandResult result, RuleViolation expected)
    {
        result.IsSuccess.ShouldBeFalse();
        result.Violation.ShouldBe(expected);
    }
}
