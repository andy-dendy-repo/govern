using Govern.UnitTests.Mediator.Fakes;
using Govern.Utils.Mediator.Pipelines;
using Govern.Utils.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Govern.UnitTests.Mediator.Pipelines;

public class ErrorHandlingPipelineTests
{
    private readonly FakeLogger<ErrorHandlingPipeline<PingRequest, PingResponse>> _logger = new();

    [Fact]
    public async Task HandleAsync_NextThrows_ReturnsUnexpectedAndLogsError()
    {
        var pipeline = new ErrorHandlingPipeline<PingRequest, PingResponse>(_logger);

        var result = await pipeline.HandleAsync(new PingRequest("hi"), () => throw new InvalidOperationException("secret detail"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(Error.Unexpected());
        _logger.LatestRecord.Level.ShouldBe(LogLevel.Error);
        _logger.LatestRecord.Exception.ShouldBeOfType<InvalidOperationException>();
        _logger.LatestRecord.Message.ShouldContain(nameof(PingRequest));
    }

    [Fact]
    public async Task HandleAsync_CallerCancelled_RethrowsCancellation()
    {
        var pipeline = new ErrorHandlingPipeline<PingRequest, PingResponse>(_logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await pipeline.HandleAsync(new PingRequest("hi"), () => throw new OperationCanceledException(cancellation.Token), cancellation.Token));

        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_NextSucceeds_ReturnsResultUnchanged()
    {
        var pipeline = new ErrorHandlingPipeline<PingRequest, PingResponse>(_logger);

        var result = await pipeline.HandleAsync(new PingRequest("hi"), () => ValueTask.FromResult<Result<PingResponse>>(new PingResponse("ok")), TestContext.Current.CancellationToken);

        result.Value.Message.ShouldBe("ok");
    }
}
