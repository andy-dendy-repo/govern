using Govern.Utils.Results;

namespace Govern.UnitTests.Results;

public class ResultTests
{
    [Fact]
    public void ImplicitValue_CreatesSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ImplicitError_CreatesFailure()
    {
        var error = Error.NotFound("thing.not_found", "Thing not found.");

        Result<int> result = error;

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        Result<int> result = Error.Conflict("thing.exists", "Thing exists.");

        var exception = Should.Throw<InvalidOperationException>(() => result.Value);

        exception.Message.ShouldContain("thing.exists");
    }

    [Fact]
    public void FromError_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Result<int>.FromError(null!));
    }

    [Theory]
    [InlineData(true, "value:7")]
    [InlineData(false, "error:boom")]
    public void Match_CallsBranchForOutcome(bool success, string expected)
    {
        var result = success ? Result<int>.FromValue(7) : Result<int>.FromError(new Error("boom", "Boom.", ErrorType.Unexpected));

        var projected = result.Match(value => $"value:{value}", error => $"error:{error.Code}");

        projected.ShouldBe(expected);
    }
}
