using Govern.Utils.Results;

namespace Govern.UnitTests.Results;

public class ErrorTests
{
    [Fact]
    public void Validation_KeepsFieldErrors()
    {
        var fields = new Dictionary<string, string[]> { ["Email"] = ["Email is required."] };

        var error = Error.Validation(fields);

        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe("validation_failed");
        error.ValidationErrors["Email"].ShouldBe(["Email is required."]);
    }

    [Fact]
    public void NonValidationErrors_HaveNoFieldErrors()
    {
        Error.NotFound("x", "y").ValidationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void TooManyRequests_HasTypeCodeAndMessage()
    {
        var error = Error.TooManyRequests("x.too_many", "Slow down.");

        error.Type.ShouldBe(ErrorType.TooManyRequests);
        error.Code.ShouldBe("x.too_many");
        error.Message.ShouldBe("Slow down.");
    }

    [Fact]
    public void Unexpected_HasGenericMessage()
    {
        var error = Error.Unexpected();

        error.Type.ShouldBe(ErrorType.Unexpected);
        error.Message.ShouldBe("An unexpected error occurred.");
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factories_SetType(Error error, ErrorType expected)
    {
        error.Type.ShouldBe(expected);
    }

    public static TheoryData<Error, ErrorType> Factories => new()
    {
        { Error.NotFound("c", "m"), ErrorType.NotFound },
        { Error.Conflict("c", "m"), ErrorType.Conflict },
        { Error.Unauthorized("c", "m"), ErrorType.Unauthorized },
        { Error.Forbidden("c", "m"), ErrorType.Forbidden },
    };
}
