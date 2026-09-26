using Govern.UnitTests.Mediator.Fakes;
using Govern.Utils.Mediator.Pipelines;
using Govern.Utils.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Govern.UnitTests.Mediator.Pipelines;

public class LoggingPipelineTests
{
    private readonly FakeLogger<LoggingPipeline<PingRequest, PingResponse>> _logger = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task HandleAsync_Success_LogsRequestNameAndElapsed()
    {
        var pipeline = new LoggingPipeline<PingRequest, PingResponse>(_logger, _time);

        await pipeline.HandleAsync(new PingRequest("secret payload"), () => Next(new PingResponse("ok")), TestContext.Current.CancellationToken);

        var record = _logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldBe("PingRequest succeeded in 25.0 ms");
    }

    [Fact]
    public async Task HandleAsync_ExpectedFailure_LogsTypeAndCodeAtDebugWithoutPayload()
    {
        var pipeline = new LoggingPipeline<PingRequest, PingResponse>(_logger, _time);

        await pipeline.HandleAsync(new PingRequest("secret payload"), () => Next(Error.Conflict("ping.duplicate", "Duplicate.")), TestContext.Current.CancellationToken);

        var record = _logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe("PingRequest failed in 25.0 ms with Conflict ping.duplicate");
        record.Message.ShouldNotContain("secret payload");
    }

    [Theory]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public async Task HandleAsync_AuthFailure_LogsWarning(ErrorType type)
    {
        var pipeline = new LoggingPipeline<PingRequest, PingResponse>(_logger, _time);

        await pipeline.HandleAsync(new PingRequest("hi"), () => Next(new Error("ping.denied", "Denied.", type)), TestContext.Current.CancellationToken);

        _logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
    }

    private ValueTask<Result<PingResponse>> Next(Result<PingResponse> result)
    {
        _time.Advance(TimeSpan.FromMilliseconds(25));
        return ValueTask.FromResult(result);
    }
}
