using Govern.UnitTests.Mediator.Fakes;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Mediator.Extensions;
using Govern.Utils.Mediator.Options;
using Govern.Utils.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.UnitTests.Mediator.Services;

public class MediatorServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_RegisteredHandler_ReturnsHandlerResponse()
    {
        await using var scope = BuildScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        var result = await mediator.SendAsync(new PingRequest("hi"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Message.ShouldBe("hi");
    }

    [Fact]
    public async Task SendAsync_Pipelines_RunInRegistrationOrderAroundHandler()
    {
        await using var scope = BuildScope(o => o.AddPipeline(typeof(FirstPipeline<,>)).AddPipeline(typeof(SecondPipeline<,>)));
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        await mediator.SendAsync(new PingRequest("hi"), Ct);

        scope.ServiceProvider.GetRequiredService<CallLog>().Calls
            .ShouldBe(["first:before", "second:before", "handler", "second:after", "first:after"]);
    }

    [Fact]
    public async Task SendAsync_PipelineReturnsFailure_HandlerIsNotCalled()
    {
        await using var scope = BuildScope(o => o.AddPipeline(typeof(ForbiddingPipeline<,>)));
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        var result = await mediator.SendAsync(new PingRequest("hi"), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
        scope.ServiceProvider.GetRequiredService<CallLog>().Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_NoHandler_ThrowsInvalidOperationException()
    {
        await using var scope = BuildScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await mediator.SendAsync(new UnhandledRequest(), Ct));

        exception.Message.ShouldContain(nameof(UnhandledRequest));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task SendAsync_UseParentScope_ControlsWhichScopeResolvesHandler(bool useParentScope, int callsInCallerScope)
    {
        await using var scope = BuildScope(o => o.UseParentScope = useParentScope);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        await mediator.SendAsync(new PingRequest("hi"), Ct);

        scope.ServiceProvider.GetRequiredService<CallLog>().Calls.Count.ShouldBe(callsInCallerScope);
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_AllRun()
    {
        await using var scope = BuildScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        await mediator.PublishAsync(new SomethingHappened(7), Ct);

        scope.ServiceProvider.GetRequiredService<CallLog>().Calls.ShouldBe(["first:7", "second:7"], ignoreOrder: true);
    }

    [Fact]
    public async Task PublishAsync_ByBaseReference_UsesRuntimeType()
    {
        await using var scope = BuildScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();
        INotification notification = new SomethingHappened(3);

        await mediator.PublishAsync(notification, Ct);

        scope.ServiceProvider.GetRequiredService<CallLog>().Calls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_Completes()
    {
        await using var scope = BuildScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediatorService>();

        await Should.NotThrowAsync(async () => await mediator.PublishAsync(new NobodyListens(), Ct));
    }

    private static AsyncServiceScope BuildScope(Action<MediatorOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<CallLog>();
        services.AddLogging();
        services.AddMediator(configure, typeof(MediatorServiceTests).Assembly);
        return services.BuildServiceProvider(validateScopes: true).CreateAsyncScope();
    }
}
