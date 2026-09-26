using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;

namespace Govern.UnitTests.Mediator.Fakes;

/// <summary>Scoped recorder shared by fake handlers and pipelines to observe call order and scopes.</summary>
public sealed class CallLog
{
    public List<string> Calls { get; } = [];
}

public sealed record PingRequest(string Message) : IRequest<PingRequest, PingResponse>;

public sealed record PingResponse(string Message) : IResponse;

internal sealed class PingHandler(CallLog log) : IRequestHandler<PingRequest, PingResponse>
{
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, CancellationToken cancellationToken)
    {
        log.Calls.Add("handler");
        return ValueTask.FromResult<Result<PingResponse>>(new PingResponse(request.Message));
    }
}

public sealed record UnhandledRequest : IRequest<UnhandledRequest, PingResponse>;

public sealed record ThrowingRequest : IRequest<ThrowingRequest, PingResponse>;

internal sealed class ThrowingHandler : IRequestHandler<ThrowingRequest, PingResponse>
{
    public ValueTask<Result<PingResponse>> HandleAsync(ThrowingRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

public sealed class FirstPipeline<TRequest, TResponse>(CallLog log) : IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    public async ValueTask<Result<TResponse>> HandleAsync(TRequest request, Func<ValueTask<Result<TResponse>>> next, CancellationToken cancellationToken)
    {
        log.Calls.Add("first:before");
        var result = await next();
        log.Calls.Add("first:after");
        return result;
    }
}

public sealed class SecondPipeline<TRequest, TResponse>(CallLog log) : IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    public async ValueTask<Result<TResponse>> HandleAsync(TRequest request, Func<ValueTask<Result<TResponse>>> next, CancellationToken cancellationToken)
    {
        log.Calls.Add("second:before");
        var result = await next();
        log.Calls.Add("second:after");
        return result;
    }
}

public sealed class ForbiddingPipeline<TRequest, TResponse> : IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    public ValueTask<Result<TResponse>> HandleAsync(TRequest request, Func<ValueTask<Result<TResponse>>> next, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<TResponse>>(Error.Forbidden("test.forbidden", "Forbidden by pipeline."));
}

public sealed record SomethingHappened(int Id) : INotification;

internal sealed class FirstSomethingHappenedHandler(CallLog log) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        log.Calls.Add($"first:{notification.Id}");
        return ValueTask.CompletedTask;
    }
}

internal sealed class SecondSomethingHappenedHandler(CallLog log) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        log.Calls.Add($"second:{notification.Id}");
        return ValueTask.CompletedTask;
    }
}

public sealed record NobodyListens : INotification;
