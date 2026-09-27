using FluentValidation;
using Govern.UnitTests.Mediator.Fakes;
using Govern.Utils.Mediator.Pipelines;
using Govern.Utils.Results;

namespace Govern.UnitTests.Mediator.Pipelines;

public class ValidationPipelineTests
{
    private bool _nextCalled;

    [Fact]
    public async Task HandleAsync_NoValidators_CallsNext()
    {
        var pipeline = new ValidationPipeline<PingRequest, PingResponse>([]);

        var result = await pipeline.HandleAsync(new PingRequest(""), Next, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CallsNext()
    {
        var pipeline = new ValidationPipeline<PingRequest, PingResponse>([new PingValidator()]);

        var result = await pipeline.HandleAsync(new PingRequest("hello"), Next, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_ReturnsFieldErrorsFromAllValidators()
    {
        var pipeline = new ValidationPipeline<PingRequest, PingResponse>([new PingValidator(), new PingLengthValidator()]);

        var result = await pipeline.HandleAsync(new PingRequest(""), Next, TestContext.Current.CancellationToken);

        _nextCalled.ShouldBeFalse();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors["Message"].ShouldBe(["Message is required.", "Message is too short."], ignoreOrder: true);
    }

    private ValueTask<Result<PingResponse>> Next()
    {
        _nextCalled = true;
        return ValueTask.FromResult<Result<PingResponse>>(new PingResponse("ok"));
    }

    private sealed class PingValidator : AbstractValidator<PingRequest>
    {
        public PingValidator() => RuleFor(r => r.Message).NotEmpty().WithMessage("Message is required.");
    }

    private sealed class PingLengthValidator : AbstractValidator<PingRequest>
    {
        public PingLengthValidator() => RuleFor(r => r.Message).MinimumLength(3).WithMessage("Message is too short.");
    }
}
