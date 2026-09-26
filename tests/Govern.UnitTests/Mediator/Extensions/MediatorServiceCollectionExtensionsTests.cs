using Govern.UnitTests.Mediator.Fakes;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Mediator.Extensions;
using Govern.Utils.Mediator.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.UnitTests.Mediator.Extensions;

public class MediatorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMediator_ScansInternalRequestAndNotificationHandlers()
    {
        var services = new ServiceCollection();

        services.AddMediator(null, typeof(MediatorServiceCollectionExtensionsTests).Assembly);

        services.ShouldContain(d => d.ServiceType == typeof(IRequestHandler<PingRequest, PingResponse>) && d.ImplementationType == typeof(PingHandler));
        services.Count(d => d.ServiceType == typeof(INotificationHandler<SomethingHappened>)).ShouldBe(2);
        services.ShouldContain(d => d.ServiceType == typeof(IMediatorService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMediator_SameAssemblyTwice_RegistersHandlersOnce()
    {
        var services = new ServiceCollection();
        var assembly = typeof(MediatorServiceCollectionExtensionsTests).Assembly;

        services.AddMediator(null, assembly, assembly);

        services.Count(d => d.ServiceType == typeof(IRequestHandler<PingRequest, PingResponse>)).ShouldBe(1);
    }

    [Fact]
    public void AddPipeline_ClosedOrNonPipelineType_Throws()
    {
        var options = new MediatorOptions();

        Should.Throw<ArgumentException>(() => options.AddPipeline(typeof(FirstPipeline<PingRequest, PingResponse>)));
        Should.Throw<ArgumentException>(() => options.AddPipeline(typeof(List<>)));
    }
}
